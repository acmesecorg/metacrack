# *catalog*

Adds character seperated data from an input text file to a new or existing catalog.

Input files should always begin with an email address. The email address is anonymized by hashing the email address and then deriving a 64bit signed integer to create a unique row id. A catalog will therefore not contain any email address information. Email addresses can however be used to derive name values using the *stem-email* and *stem-email-only* options.

Values in input files should be seperated by a ':' character.

  > *Note*<br>
  > Catalog files are implemented as a sqlite table with a number of text fields which map to the *fields* option. These files can be viewed with any sqlite compatible browser.

## Usage

`meta catalog inputpath [outputpath] [options]`
&nbsp;<br>
&nbsp;<br>

| Option | Description |
| :--- | :--- |
| -c --columns| The ordinals (positions) in the input file to map to fields where email is always at position 0.|
| -f --fields| Names of the predefined fields to write values to. Each column chosen in the *columns* option should have a matching field. Valid values are: *p password u username n name d date i number v value* |
| -t --tokenize| Turns space seperated text into sub values.|
| -n --names| Path to line seperated text file containing names to use in email stemming process when using *steam-email* or *stem-email-only* options|
| --stem-email| Use email username as a source of name values. The username portion of the email address is copied to the values field, whilst valid matches from the file specified in the *names* option are copied to the *names* field|
| --stem-email-only|Works the same as the *stem-email* option, but no other values are considered. |
| <img width=300> | |

 

## Examples
 
Given the file *input.txt*

>Email|Password|Username<br>
>alice.smith@icloud.com:password1:alice74<br>
>alice@hotmail.com:password3:alice<br>
>alice.smith+test@icloud.com:password5:alice2<br>
>alice1974@apple.com:test:test2<br>

Running the command below creates a new catalog file called metadata.db and populates the *Passwords* field with the first column of the input file. Note that the email address containing *+test* is converted to the base username form and added to the existing value in *Passwords*.

`meta catalog input.txt metadata.db`

| RowId | Passwords | Usernames | Names | Dates | Numbers | Values |
| :--- | :--- | :--- | :--- | :--- | :--- |  :--- |
|-8817702922204933476|test||||||		
|-6442325452012969502|password3||||||
|-4702923869590031925|password1:password5||||||			
|&nbsp;|||||||		

### Add usernames to the catalog
 





-columns 1 2 --fields p u -n names.txt --stem-email-only

