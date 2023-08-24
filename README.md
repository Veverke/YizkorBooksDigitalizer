# YizkorBooksDigitalizer
Downloads NYPL Yizkor Books images, OCRs them, turn book contents into a Relational DB to make it digitally searchable in an orderly manner.

# Goal
Make Yizkor Books available at New York public library - digitally searchable.
I achieve this by:
1. Automatically downloding all the book scans (each book page is a scanned image)
2. OCRing each of the scans by submitting them all to Google Cloud Vision's Text Detection api
3. Compiling a SQLite relational database out of each OCRed book page

The output (the SQLite db file) is then used by [Veverke-Genealogy-Tools](https://github.com/Veverke/Veverke-Genealogy-Tools) to allow term searches in that database (book), currently - via a Windows desktop app interface. I hope in the near future I can make this web based making this cross-platform.
