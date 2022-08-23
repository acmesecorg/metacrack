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

## Example
 
Given the file *sample.sql* located in the /tutorial folder, the following command parses the data into an *email:hash:salt* data file and an *email:username* metadata file. 

`meta sql stuff`
&nbsp;<br>
&nbsp;<br>

The following files are produced 
*sample.data.txt*
>$2a$10$XsDGiVuwaoYP8uGDoleDmuWV9s4MtMCn1OWzV3PEEFL4gtYVroNW2
>$2a$10$XsDGiVuwaoYP8uGDoleDmuWV9s4MtMCn1OWzV3PEEFL4gtYVroNW2
>$2a$10$myx7zGGnlbgRxyaPhF0NwuYkJuQ0qSHuShRpL8bQVfgGHQaIf4.Hy

*sample.meta.txt*
>password1  
>password5  
>test  			
