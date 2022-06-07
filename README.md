# XML_TO_DB_Loader - .NET library for fast load data from file to DB

##### What is XML_TO_DB_Loader?
This is simple library, that help load big amount of attributes orientated XML files.
I got a task to load hundreds of XML files with about 20 different .XSD shemas and total amount of 400 GB, some of that files were about 4 GB and need to put all of them into Postgre DB.
So i decide to write library that will have interfaces to load from any data file to  lists of c# classes (in my case it was XML) and interfaes and methods to load that list right into Data Base (in my case it was PostgreSQL).

So XML_TO_DB_Loader is a library of interfaces and methods to tranclate data from file to Data Base with realization for XML files and PostgreSQL but it can be 
extended to more file types and diffrent DataBases by your own.

How it works:
1) Create your realization for IDataTypeParser and method  
