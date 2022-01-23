# *lookup*

Takes a text file containing lines of *email:hash* or *username:hash* pairs, and prepares one or more files containing lists of associated hash and word lists for running using [hashcat](https://github.com/hashcat/hashcat) mode 9, using a previously created database or [catalog](CATALOG.md).

Input files should always begin with an email address or username identifier, and hashes (and optional salts) should be provided seperated by the ':' character e.g. 

> bob@acme.com:ab4f63f9ac65152575886860dde480a1:gb89z

When using a hash with a seperate salt, ensure that an appropriate mode option has been added. Because hashcat will fail if any hash is incorrect (causing the hash and word count files to be out of sync), it is recommended to always specify a mode. Advanced users can use the *rule* and *session* to reduce the ratio of hashes to words in the output. 

Lookup can also split files into parts using the *part* option.

## Usage

`meta lookup inputpath [catalogpath] [options]`
&nbsp;<br>
&nbsp;<br>

| Option | Description |
| :--- | :--- |
| -m --hash-type| The mode used to verify each hash before a hash / word entry is created in each file. Matches values from Hashcat.|
| -r --rule| Path to the rule file to use to remove duplicate words during processing.|
| -f --fields| The predefined fields in the catalog to read values from. Valid values are: *p password u username n name d date i number v value*. Dates and numbers are appended to other fields when specified.
| -p --part| When specified, causing a file to be broken into multiple parts. Value is specified as number of lines per file. The suffix *k* can be used to represent 1000 e.g. *300k* = 300000, *3kk* = 3000 000. Single values are interpreted implicity with a kk suffix eg *4* = 4000 000 |
| -s --sessions| Splits words for the same hash across multiple sessions. Hashes that are cracked can be removed from subsequent sessions. Extra words are placed in the last session.|
| --hash-maximum | Maximum number of words considered per hash.|
| <img width=300> | |

 

## Examples
 
Given the file *input.txt*

>Email|Password|Username<br>
>alice.smith@icloud.com:password1:alice74<br>
>alice@hotmail.com:password3:alice<br>
>alice.smith+test@icloud.com:password5:alice2<br>
>alice1974@apple.com:test:test2<br>

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

The email address of a user can be used to provide further words to be used in an associative attack. Generally they provide lower cracks than passwords or usernames, but can still be a good source of words to be used against expensive salted hashes where dictionary attacks are not desirable.

Email stemming routine requires a list of line seperated names in a text format. It removes and special characters from the email address and then uses any mathing names to generate additional values. The derived names are stored in the *Names* field, whilst the original email user is stored in the *Values* field.

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
