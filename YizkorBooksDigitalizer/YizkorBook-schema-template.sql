BEGIN TRANSACTION;
CREATE TABLE IF NOT EXISTS "Language" (
	"Id"	INTEGER,
	"Name"	TEXT,
	PRIMARY KEY("Id")
);
CREATE TABLE IF NOT EXISTS "Country" (
	"Id"	INTEGER,
	"Name"	TEXT,
	PRIMARY KEY("Id")
);
CREATE TABLE IF NOT EXISTS "Page" (
	"Id"	INTEGER,
	"BookId"	INTEGER,
	"Number"	INTEGER,
	PRIMARY KEY("Id")
);
CREATE TABLE IF NOT EXISTS "Line" (
	"Id"	INTEGER,
	"PageId"	INTEGER,
	"Number"	INTEGER,
	PRIMARY KEY("Id")
);
CREATE TABLE IF NOT EXISTS "Word" (
	"Id"	INTEGER,
	"LineId"	INTEGER,
	"Number"	INTEGER,
	"Text"	TEXT,
	PRIMARY KEY("Id")
);
CREATE TABLE IF NOT EXISTS "FirstNames" (
	"Value"	TEXT
);
CREATE TABLE IF NOT EXISTS "LastNames" (
	"Value"	TEXT
);
CREATE TABLE IF NOT EXISTS "ContextWord" (
	"Value"	TEXT
);
CREATE TABLE IF NOT EXISTS "Book" (
	"Id"	INTEGER,
	"Name"	TEXT,
	"Language"	INTEGER,
	"Country"	INTEGER,
	"Author"	TEXT,
	"Publisher"	TEXT,
	"ReleaseDate"	TEXT,
	"Pages"	INTEGER,
	"Content"	INTEGER,
	PRIMARY KEY("Id")
);
INSERT INTO "Language" ("Name") VALUES ('Hebrew'), ('Yiddish'), ('Russian');
INSERT INTO "Country" ("Name") VALUES ('Israel'), ('USA'), ('Argentina'), ('Australia');
COMMIT;
