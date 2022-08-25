# *sql*

Imports data from sql exports file(s) containing lists of INSERT statements. Various Sql variants are supported including MySql and Transact Sql.

## Usage

`meta sql inputpath [options]`
&nbsp;<br>
&nbsp;<br>

| Option | Description |
| :--- | :--- |
| --table| Table definition to import data from. Default *users*.|
| --columns| The space seperated columns indexes in the VALUES of the INSERT statment.|
| --column-names| The names of the columns to be imported (if they exist in the INSERT statment). Use instead of --columns|
| --meta| The indicies of the columns to be used to write data to a meta data file. The first column (from --column or --column-names) is used as a common key |
| --meta-column-names| The names of the columns to be used for meta data, instead of --meta column indicies.|
| --start | Line in the input file to being parsing. If not specified, the whole file is parsed.|
| --end | Line in the input file to end parsing. If not specified, the file is parsed to the end.|
| --debug | Shows parse information on screen without processing file further.|
| --modes | Modifies processing depending on the mode chosen. (experimental)|
| <img width=350> | |			

 
## Examples

Included in the /tutorial folder is *sample1.sql*. We can parse the data from sample1 with this command:

```
INSERT INTO MyUsers<br>
    (Id, Email, Hash, Username, Salt)
VALUES
    (1, 'joe@blogs.com', 'e76e194b7b75987f1bc8b54dc9349277', 'Joe', 'c808zlYhCO5vBQRagXEhDZGsVlvJv0')
    (2, 'joe@soap.com', '7fa65c550919b2a45b82131eda56e9de', 'Soapster', 'BBT1ezmsegtaEkJStRbd8uRPkcPfiP')
    (3, 'joe@blow.org', 'f3ae38b17addc9efc539e719554d0b87', 'movieguy', 'f3ae38b17addc9efc539e719554d0b87')
```

`meta sql sample1.sql --table MyUsers --columns 1 2 4 --meta 3`
&nbsp;<br>
&nbsp;<br>

This puts the *email*:*hash*:*salt* for each insert into the *sample1.parsed.txt* file, and the *email*:*username* into the *sample1.meta.txt* file as follows:

*sample1.parsed.txt*
>joe<span>@blogs.com:e76e194b7b75987f1bc8b54dc9349277:c808zlYhCO5vBQRagXEhDZGsVlvJv0<br>
>joe<span>@soap.com:7fa65c550919b2a45b82131eda56e9de:BBT1ezmsegtaEkJStRbd8uRPkcPfiP<br>
>joe<span>@blow.org:f3ae38b17addc9efc539e719554d0b87:f3ae38b17addc9efc539e719554d0b87<br>

*sample1.meta.txt*
>joe<span>@blogs.com:Joe<br>
>joe<span>@soap.com:Soapster<br>
>joe<span>@blow.org:movieguy<br>
 
You can also use the `--debug` option to display the first few lines of parsed sql, without writing a file. This can help confirm that the right columns have been selected.

 
 ### Specifying column names instead of indices
 
 Because the INSERT statement contains the column names, we can achieve the same result using this command. Note that we use `--column--names` and `--meta-column-names` instead of `--columns` and `--meta`

 `meta sql sample1.sql --table MyUsers --column-names Email Hash Salt --meta-column-names Username`
 
 


