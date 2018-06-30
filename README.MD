# KEY POINTS #

**It is *NOT* an end-user ready-to-use application**. So **you need at least basic C# language knowledge** to use it for the goal it was intended for.  

This project was initially started by me to have a **tool for easy comprasion of file records in a torrent, which are located in several folders**.  
The goal was to make this fast and pretty simple, just as opening a torrent and getting all file records (as list of strings) from it by writing 2-3 lines of C# code.  
All code was supposed to be *safe*. The choice of methods and done optimizations was based at the maxing application work speed, not memory consumption.  

I'm not a professional developer (nevertheless I get a lot of pleasure learning and developing). So this code can break some C# and generic programming guidelines. Some choose methods could be not optimal, and some tasks could be done in a more easy/elegant way.
If you can suggest more elegant/correct solutions for different project tasks (with a comment *why it is better*), I would be grateful to you for improving my language knowledge.  


# HOW TO USE #

Just take a look at the **SampleProject**. It is provided with rather detailed comments.  
To use it in your project you need to include the **BEncodeParser** class (from the *TorrentParser* project) or compile it as a library and add a reference to it.  


# LIMITATIONS #

* Max torrent size is 2GB.  
*The biggest torrent file I have seen was 7,651 KB, so I assumed that this can fit most torrent files you want to handle.*
* The BitTorrent specification items which are not realised:
  * Numbers such as `i-0e` or `i03e` are not treated as incorrect ones.
  * Keys in a BEncode dictionary must be sorted in ascending order using binary comparison. This is not checked while parsing.
  * One-file torrent has a bit different structure unlike a multi-file torrent.  
  *Since the main goal of this project was to compare several files inside one torrent, this difference wasn't realised.*
* Torrent file must consist of a single dictionary with several items. This is not checked while parsing. So a parsed file can include several `numbers` or `strings` following each other outside of any list or dictionary.
* The class code doesn't process exceptions by itself, it just throws them to user.  


# CHANGES #

* 1.0 — 09.07.2017 — First release.
