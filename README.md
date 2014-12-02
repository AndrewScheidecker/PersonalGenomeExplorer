This is the new home for a program I initially wrote in December 2007 to cross-reference my genome data from 23andme with the SNP data available from SNPedia. The source has been available on my website, but I decided to put it up on Github.

I took the opportunity to upgrade it to the latest version of C#/.NET, and made a few improvements behind the scenes:
* C#5 async/await is now used to keep the UI responsive during long-running background tasks like downloading the SNPedia database.
* Downloading the SNPedia database now uses the Wikimedia query API to download 50 pages per HTTP request, which is much faster than the old approach downloading 1 page/request.

An updated SNPedia database is included, so a typical user won't see these improvements.

For more information from my original work on the program, see https://www.scheidecker.net/personal-genome-explorer/