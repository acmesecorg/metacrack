# *catalog*

Adds character seperated data from an input text file to a new or existing database.

Input files should always begin with an email address. The email address is anonymized by hashing the email address and then deriving a 64bit signed integer to create a unique row id. A database will therefore not contain any email address information. Email addresses can however be used to derive name values using the *stem-email* and *stem-email-only* options, however this may substantially de-anonimise the data.

Values in input files should be seperated by a ':' character.

  > **Note**<br>
  > Database files are implemented as a sqlite table with a number of text fields which map to the *fields* option. These files can be viewed with any sqlite compatible browser.

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
>alice.smith<span>@icloud.com:password1:alice74<br>
>alice<span>@hotmail.com:password3:alice<br>
>alice.smith+test<span>@icloud.com:password5:alice2<br>
>alice1974<span>@apple.com:test:test2<br>

Passwords are often reused by users and they are the most common type of meta data used to create an associative attack. Between 10% and 25% records in a data breach may contain reused passwords or passwords that can be matched with a simple rule.

Running the command below creates a new catalog file called metadata.db and populates the *Passwords* field with the first column of the input file. Note that the email address containing *+test* is converted to the base username form and added to the existing value in *Passwords*.

`meta catalog input.txt metadata.db`
&nbsp;<br>
&nbsp;<br>

| RowId | Passwords | Usernames | Names | Dates | Numbers | Values |
| :--- | :--- | :--- | :--- | :--- | :--- |  :--- |
|-8817702922204933476|test||||||		
|-6442325452012969502|password3||||||
|-4702923869590031925|password1:password5||||||					

### Add usernames to the catalog

Usernames can often form the basis of a user password, and can deliver a considerable amount of cracks when used with a more complicated ruleset, approaching the amount yielded when using a known passwords from other breaches.

To add the usernames contained in the second column, we can reprocess the file specifying the columns and fields we want to use. There should always be a field specified for every column. When ommitted, the first column is mapped to the Passwords field as per the example above. No new passwords were added because they already existed in the catalog.

`meta catalog input.txt metadata.db --columns 1 2 --fields p u`
&nbsp;<br>
&nbsp;<br>

| RowId | Passwords | Usernames | Names | Dates | Numbers | Values |
| :--- | :--- | :--- | :--- | :--- | :--- |  :--- |
|-8817702922204933476|test|test2|||||		
|-6442325452012969502|password3|alice|||||
|-4702923869590031925|password1:password5|alice74:alice2|||||			

### Stemming the email address for further values

The email address of a user can be used to provide further words to be used in an associative attack. Generally they provide lower cracks than passwords or usernames, but can still be a good source of words to be used against expensive salted hashes where dictionary attacks are not desirable, especially when combined with dates and numbers in the catalog, or when using a rule.

The email stemming processor requires a list of line seperated names in a text format. It removes special characters from the email address and then uses any matches to generate additional values. The derived names are stored in the *Names* field, whilst the original email user is stored in the *Values* field.

Given the file *names.txt* and running the command following:

>Alice<br>
>Bob<br>

`meta catalog input.txt metadata.db --columns 1 2 --fields p u --names names.txt --stem-emails`
&nbsp;<br>
&nbsp;<br>

| RowId | Passwords | Usernames | Names | Dates | Numbers | Values |
| :--- | :--- | :--- | :--- | :--- | :--- |  :--- |
|-8817702922204933476|test|test2|alice||1974|alice1974|	
|-6442325452012969502|password3|alice|alice|||alice|
|-4702923869590031925|password1:password5|alice74:alice2|alice:smith|||alice.smith|			
