using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace TorrentParser
{
    /// <summary>
    /// Performs all operations on torrent files.
    /// </summary>
    public class BEncodeParser
    {
        class BinaryData
        {
            public byte[] BinData { get; private set; }
            public Encoding UsedEncoding;
            public string StringData
            {
                get
                {
                    try
                    {
                        return UsedEncoding.GetString(BinData);
                    }
                    catch (Exception)
                    {
                        return base.ToString();
                    }
                }
            }
            public BinaryData(byte[] binaryData, Encoding encoding)
            {
                BinData = binaryData;
                UsedEncoding = encoding;
            }
            public override string ToString()
            {
                try
                {
                    return UsedEncoding.GetString(BinData);
                }
                catch (Exception)
                {
                    return base.ToString();
                }
            }
            public override bool Equals(object obj)
            {
                if (obj == null)
                    return false;

                if (obj is BinaryData)
                {
                    return BinData.Equals(((BinaryData)obj).BinData);
                }
                else if (obj is byte[])
                {
                    return BinData.Equals(obj);
                }
                else if (obj is string)
                {
                    return ToString().Equals(obj);
                }
                else
                {
                    return false;
                }
            }
            //for extracting 'value' from dictionary by using 'key' (keys are compared by hash sum)
            public override int GetHashCode()
            {
                //TODO: FUTURE: alternative - GetHashCode of each byte in the array, merge them. получение значения из словаря по ключу-строке
                //  станет невозможным.
                return ToString().GetHashCode();
            }
        }
        struct ElementKeySymbols
        {
            //byte[] StartSymbols - all chars which would be recognized as opening of the element.
            public byte[] StartSymbols;
            //byte[] NextSymbols - after opening the element, program would expect one of these chars as the next symbol in the string.
            //  If not - exception would be raised.
            public byte[] NextSymbols;
            //byte EndSymbol - [b]byte[/b] value of [b]char[b]. Meeting that char, while some element is opened, would close that element.
            public byte EndSymbol;
            //byte Delimeter - [b]byte[/b] value of [b]char[b] used as a delimeter in the 'length' type elements.
            public byte Delimeter;
        }
        [Flags]
        enum ElementType
        {
            None = 0x0,
            //can be 'long' type
            Number = 0x1,
            //TODO: FUTURE: add compability for long strings
            //assumed that it cannot be larger than 'int32'. So max torrent file size is 2GB.
            //  The biggest torrent file I have seen was 7,651 KB, so I assumed that this can
            //  fit most torrent files you want to handle.
            ByteString = 0x2,
            List = 0x4,
            Dic = 0x8,
            Unknown = 0x10
        }
        class PendingElement
        {
            public ElementType Type { get; }
            public byte[] BinaryData;
            //'ByteString' max length is supposed to be int32.MaxValue, 'Number' treated as 'long' type can consists of maximum 20 chars (including negative sign).
            public int BinaryDataSize;
            public List<object> InnerList { get; }
            public PendingElement(ElementType type)
            {
                Type = type;
                //Initial size. It will be enough to store numbers that represent up to 9GB. Later in the code it can grow up if its size wouldn't enough.
                BinaryData = new byte[10];
                BinaryDataSize = 0;
                //TODO: FUTURE: найти более изящное решение создания элемента словаря
                //used exclusively for creating a dictionary Key-Value pair
                InnerList = new List<object>(2);
            }
        }
        
        //fields which are general to all files
        Dictionary<ElementType, ElementKeySymbols> parserElementsTypes;
        const int parserBufferSize = 4096;
        Encoding parserLexEncoding, parserStringEncoding;
        Dictionary<byte, ElementType> parserCharToElementType;
        Dictionary<byte, ElementType> parserNextCharAllowedTypes;
        Func<char, byte> ParserCharToByte;
        Func<char[], byte[]> ParserCharsToBytes;
        //not used
        Func<byte, char> ParserByteToChar;
        Func<byte[], int> ParserBytesToInt;
        const int parserASCIIZero = 48;
        //used to determine whether 'encoding' and 'pieces' strings are processing now
        byte[] parserEncodingKey, parserPiecesKey;

        //fields which are needed to be cleaned for each new processed file
        Stack<PendingElement> fileElementsStack;
        //set to 'True' when pieces string would be add to dictionary
        bool filePiecesEncoded;
        byte[] fileByteArray;
        //app can handle torrent files smaller than 2GB
        int fileByteArrayLength;
        int fileByteArrayOffset;
        Encoding filePiecesEncoding;

        /// <summary>
        /// Initializes a new instance of <see cref="BEncodeParser"/> class which uses ASCII for lexicon parsing and UTF-8 for string decoding.
        /// </summary>
        public BEncodeParser()
        {
            InitFileFields();
            //bencode uses ASCII for own lexicon
            parserLexEncoding = Encoding.ASCII;
            //All strings are UTF8 encoded. 'encoding' string contains encoding used for 'pieces' string of 'info' dictionary.
            parserStringEncoding = Encoding.UTF8;
            //stringEncoding = piecesEncoding = Encoding.UTF8;
            ParserCharToByte = x => parserLexEncoding.GetBytes(new char[] { x })[0];
            ParserCharsToBytes = x => parserLexEncoding.GetBytes(x);
            ParserByteToChar = x => parserLexEncoding.GetChars(new byte[] { x })[0];
            //it will raise OverflowException if constructed number is bigger than int.MaxValue
            ParserBytesToInt = x => {
                long l = BytesToLong(x);
                if (l > int.MaxValue || l < int.MinValue)
                    throw new Exception("Number '" + l + "' is bigger or less than int32 max values.");
                return (int)l;
            };

            parserEncodingKey = parserStringEncoding.GetBytes("encoding");
            parserPiecesKey = parserStringEncoding.GetBytes("pieces");
            byte[] digitsArray = ParserCharsToBytes(new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' });
            
            parserElementsTypes = new Dictionary<ElementType, ElementKeySymbols>()
            {
                { ElementType.Number, new ElementKeySymbols()
                    {
                        StartSymbols = new byte[] { ParserCharToByte('i') },
                        NextSymbols = digitsArray.Concat(new byte[] { ParserCharToByte('-') }).ToArray<byte>(),
                        EndSymbol = ParserCharToByte('e')
                    }
                },
                { ElementType.ByteString, new ElementKeySymbols()
                    {
                        StartSymbols = digitsArray,
                        NextSymbols = digitsArray,
                        Delimeter = ParserCharToByte(':')
                    }
                },
                { ElementType.List, new ElementKeySymbols()
                    {
                        StartSymbols = new byte[] { ParserCharToByte('l') },
                        NextSymbols = digitsArray.Concat(ParserCharsToBytes(new char[] { 'l', 'd', 'i' })).ToArray<byte>(),
                        EndSymbol = ParserCharToByte('e')
                    }
                },
                { ElementType.Dic, new ElementKeySymbols()
                    {
                        StartSymbols = new byte[] { ParserCharToByte('d') },
                        EndSymbol = ParserCharToByte('e')
                    }
                }
            };
            //for performance reason we build two dictionary which will help to determine 'ElementType' by providing to them first or next char
            parserCharToElementType = new Dictionary<byte, ElementType>();
            parserNextCharAllowedTypes = new Dictionary<byte, ElementType>();
            foreach (KeyValuePair<ElementType, ElementKeySymbols> kvp in parserElementsTypes)
            {
                foreach (byte b in kvp.Value.StartSymbols)
                {
                    parserCharToElementType.Add(b, kvp.Key);
                }

                if (kvp.Value.NextSymbols != null)
                {
                    foreach (byte b in kvp.Value.NextSymbols)
                    {
                        if (!parserNextCharAllowedTypes.ContainsKey(b))
                            parserNextCharAllowedTypes.Add(b, kvp.Key);
                        else
                        {
                            parserNextCharAllowedTypes[b] |= kvp.Key;
                        }
                    }
                }
            }
        }
        TorrentObject BuildTorrent(byte[] byteArrayFromFile)
        {
            object parsed = ParseFile(byteArrayFromFile);
            return BuildTorrent(parsed);
        }
        /// <summary>
        /// Parses torrent file at a specified location and returns in a human-readable format.
        /// </summary>
        /// <param name="filePath">A full path to parsed torrent file or only file name if torrent file and the app executable are in the same folder.</param>
        /// <returns>Constructed TorrentObject from parsed torrent file at filepath</returns>
        public TorrentObject BuildTorrent(string filePath)
        {
            object parsed = ParseFile(filePath);
            return BuildTorrent(parsed);
        }
        TorrentObject BuildTorrent(object bencodeObject)
        {
            if (bencodeObject is Dictionary<object, object>)
                return new TorrentObject((Dictionary<object, object>)bencodeObject);
            else
                throw new Exception("Parsed object is not BEncode dictionary!");
        }
        long BytesToLong(byte[] byteArray)
        {
            long integer64 = 0;
            bool containsNull = false;
            //'Number' treated as 'long' type can consists of maximum 20 chars (including negative sign).
            bool firstByte = true;
            bool positive = true;
            foreach (byte b in byteArray)
            {
                //Nubmer is wroted as a sequence of bytes, each byte represents one ASCII digit,
                //   so we don't need to consider endianness and possible leading/trailing null byte 0x00.
                //But byteArray is an array of predefined size. If its size less than number of digits,
                //  free indexes/positions would be null.

                //TODO: FUTURE: can be optimized to exit cycle when first null byte is found
                if (b == 0)
                {
                    if (firstByte)
                        throw new Exception("Failed to construct Integer! Invalid binary array contains 0x00 byte!");
                    if (!containsNull)
                        containsNull = true;
                }
                else if (containsNull)
                { 
                    throw new Exception("Failed to construct Integer! Invalid binary array contains 0x00 byte!");
                }
                else
                {
                    if (firstByte && b == 45)
                        positive = false;
                    else if (b < 48 && b > 57)
                        throw new Exception("Failed to construct Integer! Non-digit in char array!");
                    else
                        integer64 = integer64 * 10 + (b - parserASCIIZero);
                }
                firstByte = false;
            }
            return (positive ? integer64 : integer64 * -1);
        }
        object ConstructElement (byte symbol)
        {
            PendingElement peekedElement = null;
            //Getting last element. After reading next/new element, it would be treated as previous one.
            if (fileElementsStack.Count > 0)
                peekedElement = fileElementsStack.Peek();

            ElementType elementType;
            if (!parserCharToElementType.TryGetValue(symbol, out elementType))
                throw new Exception("Invalid syntax or Unknown type!");

            if (peekedElement != null && peekedElement.Type == ElementType.Dic && peekedElement.InnerList.Count == 0 && elementType != ElementType.ByteString)
                throw new Exception("Dictionary element wasn't opened with a ByteString as a key!");

            //about 13% of execution time
            fileElementsStack.Push(new PendingElement(elementType));
            //Now it is a new/current element.
            peekedElement = fileElementsStack.Peek();
            object tempObject = null;
                    
            switch (elementType)
            {
                case ElementType.Number:
                    break;
                case ElementType.ByteString:
                    if (peekedElement.BinaryDataSize == peekedElement.BinaryData.Length)
                    {
                        byte[] biggerArray = new byte[peekedElement.BinaryDataSize * 2];
                        Buffer.BlockCopy(peekedElement.BinaryData, 0, biggerArray, 0, peekedElement.BinaryDataSize);
                        peekedElement.BinaryData = biggerArray;
#if DEBUG
                        Console.WriteLine("Array's size was increased!");
#endif
                    }
                    peekedElement.BinaryData[peekedElement.BinaryDataSize] = symbol;
                    peekedElement.BinaryDataSize++;
                    break;
                case ElementType.List:
                    tempObject = new List<object>();
                    break;
                case ElementType.Dic:
                    tempObject = new Dictionary<object, object>();
                    break;
                default:
                    throw new Exception("Invalid syntax or Unknown type!");
                    break;
            }

            bool isFinished = false;
            while (!isFinished)
            {
                if (fileByteArrayOffset == fileByteArrayLength) throw new Exception("Invalid syntax!");
                symbol = fileByteArray[fileByteArrayOffset++];

                switch (peekedElement.Type)
                {
                    case ElementType.Number:
                        if (parserNextCharAllowedTypes.TryGetValue(symbol, out elementType) && ((peekedElement.Type & elementType) == peekedElement.Type))
                        {
                            if (peekedElement.BinaryDataSize == peekedElement.BinaryData.Length)
                            {
                                byte[] biggerArray = new byte[peekedElement.BinaryDataSize * 2];
                                Buffer.BlockCopy(peekedElement.BinaryData, 0, biggerArray, 0, peekedElement.BinaryDataSize);
                                peekedElement.BinaryData = biggerArray;
#if DEBUG
                                Console.WriteLine("Array's size was increased!");
#endif
                            }
                            peekedElement.BinaryData[peekedElement.BinaryDataSize] = symbol;
                            peekedElement.BinaryDataSize++;
                        }
                        else if (parserElementsTypes[peekedElement.Type].EndSymbol == symbol)
                        {
                            isFinished = true;
                        }
                        else
                            throw new Exception("Invalid syntax!");
                        break;
                    case ElementType.ByteString:
                        if (parserNextCharAllowedTypes.TryGetValue(symbol, out elementType) && ((peekedElement.Type & elementType) == peekedElement.Type))
                        {
                            if (peekedElement.BinaryDataSize == peekedElement.BinaryData.Length)
                            {
                                byte[] biggerArray = new byte[peekedElement.BinaryDataSize * 2];
                                Buffer.BlockCopy(peekedElement.BinaryData, 0, biggerArray, 0, peekedElement.BinaryDataSize);
                                peekedElement.BinaryData = biggerArray;
#if DEBUG
                                Console.WriteLine("Array's size was increased!");
#endif
                            }
                            peekedElement.BinaryData[peekedElement.BinaryDataSize] = symbol;
                            peekedElement.BinaryDataSize++;
                        }
                        else if (parserElementsTypes[peekedElement.Type].Delimeter == symbol)
                        {
                            isFinished = true;
                        }
                        else
                            throw new Exception("Invalid syntax!");
                        break;
                    case ElementType.List:
                        //TODO: FUTURE: nextCharAllowedTypes слишком сложен по сравнению с elementTypes[].EndSymbol
                        if (parserNextCharAllowedTypes.TryGetValue(symbol, out elementType) && ((peekedElement.Type & elementType) == peekedElement.Type))
                        {
                            ((List<object>)tempObject).Add(ConstructElement(symbol));
                        }
                        else if (parserElementsTypes[peekedElement.Type].EndSymbol == symbol)
                        {
                            isFinished = true;
                        }
                        else
                            throw new Exception("Invalid syntax!");
                        break;
                    case ElementType.Dic:
                        if (parserElementsTypes[peekedElement.Type].EndSymbol == symbol)
                        {
                            isFinished = true;
                        }
                        else
                        {
                            peekedElement.InnerList.Add(ConstructElement(symbol));
                            if (peekedElement.InnerList.Count == 2)
                            {
                                if (filePiecesEncoding == null && ((BinaryData)peekedElement.InnerList[0]).BinData.Equals(parserEncodingKey))
                                    filePiecesEncoding = Encoding.GetEncoding(peekedElement.InnerList[1].ToString());
                                if (!filePiecesEncoded && ((BinaryData)peekedElement.InnerList[0]).BinData.Equals(parserPiecesKey))
                                {
                                    filePiecesEncoded = true;
                                    if (filePiecesEncoding != null)
                                        ((BinaryData)peekedElement.InnerList[1]).UsedEncoding = filePiecesEncoding;
                                }
                                ((Dictionary<object, object>)tempObject).Add(peekedElement.InnerList[0], peekedElement.InnerList[1]);
                                //pair Key-Value is created, clear list for next pair
                                peekedElement.InnerList.Clear();
                            }                            
                        }
                        break;
                    default:
                        throw new Exception("Invalid syntax!");
                        break;
                }
            }
            //when element construction is finished
            switch (peekedElement.Type)
            {
                //TODO: FUTURE: доработать: i-0e и i03e некорректны. Ключи в словаре отсортированы, используя бинарное сравнение.
                case ElementType.Number:
                    //'Number' can be 'long' type
                    fileElementsStack.Pop();
                    return BytesToLong(peekedElement.BinaryData);
                case ElementType.ByteString:
                    //assumed that it cannot be larger than int32
                    int stringSize = ParserBytesToInt(peekedElement.BinaryData);
                    //BinaryData was used to store string length. As we got string length, it is no longer needed.
                    peekedElement.BinaryData = new byte[stringSize];
                    peekedElement.BinaryDataSize = 0;
                    Buffer.BlockCopy(fileByteArray, fileByteArrayOffset, peekedElement.BinaryData, 0, stringSize);
                    fileByteArrayOffset += stringSize;

                    fileElementsStack.Pop();
                    return new BinaryData(peekedElement.BinaryData, parserStringEncoding);
                case ElementType.List:
                case ElementType.Dic:
                    fileElementsStack.Pop();
                    return tempObject;
                default:
                    throw new Exception("Invalid syntax or Unknown type!");
                    break;
            }
        }
        void InitFileFields()
        {
            fileElementsStack = null;
            //set to 'True' when pieces string would be add to dictionary
            filePiecesEncoded = false;
            fileByteArray = null;
            //app can handle torrent files smaller than 2GB
            fileByteArrayLength = 0;
            fileByteArrayOffset = 0;
            filePiecesEncoding = null;
        }
        /// <summary>
        /// Parses torrent file at a specified location and returns decoded objects in an unmodified torrent model.
        /// </summary>
        /// <param name="filePath">A full path to parsed torrent file or only file name if torrent file and the app executable are in the same folder.</param>
        /// <returns>Parsed BEncode objects as base types: long, Dictionary, List; and 'BEncode strings' as <see cref="BEncodeParser.BinaryData"/> instances.</returns>
        public object ParseFile(string filePath)
        {
            byte[] internalBuffer;
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, parserBufferSize))
            using (BinaryReader br = new BinaryReader(fs))
            {
                int streamLength = 0;
                try
                {
                    streamLength = Convert.ToInt32(br.BaseStream.Length);
                }
                catch (OverflowException)
                {
                    throw new Exception("Torrent files bigger than 2GB are not supported!");
                }
                internalBuffer = new byte[streamLength];
                int internalBufferSize = 0, readBytes = 0, remainBytes = streamLength;

                //int.MaxValue is used for files bigger than 2GB
                //while ((readBytes = br.Read(internalBuffer, internalBufferSize, (int)(remainBytes > int.MaxValue ? int.MaxValue : remainBytes))) > 0)
                
                //we use FileStream with a buffer set to 4096
                while ((readBytes = br.Read(internalBuffer, internalBufferSize, remainBytes)) > 0)
                {
                    internalBufferSize += readBytes;
                    remainBytes -= readBytes;
                }
            }

            return ParseFile(internalBuffer);
        }
        object ParseFile(byte[] byteArrayFromFile)
        {
            fileElementsStack = new Stack<PendingElement>();
            object bencodeObject;
            fileByteArray = byteArrayFromFile;
            fileByteArrayLength = fileByteArray.Length;
            fileByteArrayOffset = 0;
            /*алгоритм2:
            Перебираем char в файле.
            LookStartSymbol должна искать текущий char в StartSymbols всех элементов.
            Первое совпадение определяет тип элемента.
            Создаем в stack Элемент определенного типа.
            Берем следующий char из файла.
            Если элемент не закрыт, то над следующим чаром уже выполняем функцию Element.NextSymbol, которая индивидуальна дла каждого типа элемента.
            Эта функция должна проверять char на заключительный, если true, то закрывать элемент и записывать его значение в object.
            Если не удается открыть элемент (нет совпадений), либо продолжить или закрыть, то вызывать exception типа SyntaxError. Парсинг не продолжать.
            */
            
            //TODO: FUTURE: первый элемент - единственный корневой элемент. Сделать проверки на несколько элементов вне листов и словарей.
            bencodeObject = ConstructElement(fileByteArray[fileByteArrayOffset++]);

            if (fileElementsStack.Count > 0) throw new Exception("Not all elements are closed!");
            //clean all file-dependent fields releasing resources and preparing to new file
            InitFileFields();
            return bencodeObject;
        }
    }
    /// <summary>
    /// Represents parsed torrent file in a human-readable format.
    /// </summary>
    public class TorrentObject
    {
        /// <summary>
        /// Structure that represents file record from 'info' dictionary in a torrent file.
        /// </summary>
        public struct FileStruct
        {
            /// <summary>Length of the file in bytes.</summary>
            public long Length;
            /// <summary>(optional) A byte array representing the MD5 sum of the file.</summary>
            public byte[] Md5sum;
            /// <summary>A list containing one or more <see cref="BEncodeParser.BinaryData"/> elements that together represent the path and filename.</summary>
            public List<object> Path;
            /// <summary>The filename only (the final element of the <see cref="FileStruct.Path"/> object).</summary>
            public string FileName;
            /// <summary>A combination of all elements in the <see cref="FileStruct.Path"/> object.</summary>
            public string FullPath;

            /// <summary>Returns <see cref="FileStruct.FullPath"/> as a string representation of this object.</summary>
            public override string ToString()
            {
                return FullPath;
            }
        }
        /// <summary>
        /// Strcucture that represents 'info' dictionary in a torrent file.
        /// </summary>
        public struct InfoStruct
        {
            /// <summary>A 'name' string of 'info' dictionary in a torrent. In multi-file mode it is name of folder which contains all files.</summary>
            public string Name;
            /// <summary>A 'files' list of 'info' dictionary in a torrent.</summary>
            public List<FileStruct> Files;
        }

        /// <summary>An 'info' dictionary in a torrent.</summary>
        public InfoStruct Info;
        //TODO: FUTURE: не закочено. Торренты с одним файлом отличаются структурой.
        public TorrentObject(Dictionary<object, object> dic)
        {
            string infoKey = "info";
            string filesKey = "files";

            this.Info = new InfoStruct();
            this.Info.Files = new List<FileStruct>();
            foreach (Dictionary<object, object> fileAsDic in (List<object>)((Dictionary<object, object>)dic[infoKey])[filesKey])
            {
                List<object> pathObject = (List<object>)fileAsDic["path"];
                this.Info.Files.Add(
                    new FileStruct()
                    {
                        Length = (long)fileAsDic["length"],
                        Path = pathObject,
                        FileName = pathObject.Last().ToString(),
                        FullPath = string.Join("\\", pathObject)
                    }
                );
            }

            /*алгоритм:
            Запрашиваем в корневом словаре ключ 'info'.
            Получаем dic.
            Запрашиваем ключ 'files'.
            + Получаем list.
            Каждый объект list - файл - словарь (содержащий два ключа 'length' и 'path').
            'path' каждого файла - это list.
            В нем каждый элемент это часть пути (директория) без разделителей.
            Последний элемент - имя файла.
            'length' каждого файла - это 'long' - размер файла.
            */
        }
    }
}
