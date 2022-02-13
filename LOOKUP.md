# *lookup*

Takes a text file containing lines of *email:hash* or *username:hash* pairs, and prepares one or more files containing lists of associated hash and word lists for running using [hashcat](https://github.com/hashcat/hashcat) mode 9, using a previously created database or [catalog](CATALOG.md).

Input files should always begin with an email address or username identifier, and hashes (and optional salts) should be provided seperated by the ':' character e.g. 

> bob<span>@acme.com:ab4f63f9ac65152575886860dde480a1:gb89z

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
| <img width=350> | |

 

## Examples
 
Given the file *breach.txt*

>alice.smith<span>@icloud.com:$2a$10$XsDGiVuwaoYP8uGDoleDmuWV9s4MtMCn1OWzV3PEEFL4gtYVroNW2
>alice1974<span>@apple.com:$2a$10$myx7zGGnlbgRxyaPhF0NwuYkJuQ0qSHuShRpL8bQVfgGHQaIf4.Hy

Running the command below creates a hash and word file pair named *breach.hash* and *breach.word*. 

`meta lookup breach.txt metadata.db -m 3200`
&nbsp;<br>
&nbsp;<br>

*breach.hash*
>$2a$10$XsDGiVuwaoYP8uGDoleDmuWV9s4MtMCn1OWzV3PEEFL4gtYVroNW2
>$2a$10$XsDGiVuwaoYP8uGDoleDmuWV9s4MtMCn1OWzV3PEEFL4gtYVroNW2
>$2a$10$myx7zGGnlbgRxyaPhF0NwuYkJuQ0qSHuShRpL8bQVfgGHQaIf4.Hy

*breach.word*
>password1  
>password5  
>test  			

These files have the same length and the hash has been validated so that hashcat can now be used in associative mode e.g.

`hashcat -a 9 -m 3200 breach.hash breach.word -o breach.output.txt`
&nbsp;<br>
&nbsp;<br>

	
### Including extra fields in the output
	
To use other fields from our database catalog, we need to specify which fields to use. The [catalog](https://github.com/metacrackorg/metacrack/blob/sqlite/CATALOG.md) example shows how to add various forms of meta data to the catalog. Using the *-f* or *--fields** option, we can also include these values as words in our output.

The following command uses passwords, usernames, and numbers from the catalog:

`meta lookup breach.txt metadata.db -m 3200 -f p u i`
&nbsp;<br>
&nbsp;<br>
	
*breach.hash*
>$2a$10$XsDGiVuwaoYP8uGDoleDmuWV9s4MtMCn1OWzV3PEEFL4gtYVroNW2
>$2a$10$XsDGiVuwaoYP8uGDoleDmuWV9s4MtMCn1OWzV3PEEFL4gtYVroNW2
>$2a$10$XsDGiVuwaoYP8uGDoleDmuWV9s4MtMCn1OWzV3PEEFL4gtYVroNW2
>$2a$10$XsDGiVuwaoYP8uGDoleDmuWV9s4MtMCn1OWzV3PEEFL4gtYVroNW2
>$2a$10$myx7zGGnlbgRxyaPhF0NwuYkJuQ0qSHuShRpL8bQVfgGHQaIf4.Hy
>$2a$10$myx7zGGnlbgRxyaPhF0NwuYkJuQ0qSHuShRpL8bQVfgGHQaIf4.Hy
>$2a$10$myx7zGGnlbgRxyaPhF0NwuYkJuQ0qSHuShRpL8bQVfgGHQaIf4.Hy
>$2a$10$myx7zGGnlbgRxyaPhF0NwuYkJuQ0qSHuShRpL8bQVfgGHQaIf4.Hy

*breach.word*
>password1 
>password5 
>alice74   
>alice2    
>test  
	
	
### Using a rule to cut down on repetitions

In the previous example, the words *password1* and *password5* were returned for the same hash. Although this is sometimes unavoidable, it is far more efficient to use a rule inside hashcat then to specify similar guesses for a hash.

Re-run the command but this time specify a rule:
	
  > **Note**<br>
  > The *best64.rule* file can be found inside the /rules folder of your hashcat installation or from the GitHub repository [here](https://github.com/hashcat/hashcat/blob/master/rules/best64.rule). 

`meta lookup breach.txt metadata.db -m 3200 -r best64.rule`
&nbsp;<br>
&nbsp;<br>
	
*breach.hash*
>$2a$10$XsDGiVuwaoYP8uGDoleDmuWV9s4MtMCn1OWzV3PEEFL4gtYVroNW2
>$2a$10$myx7zGGnlbgRxyaPhF0NwuYkJuQ0qSHuShRpL8bQVfgGHQaIf4.Hy

*breach.word*
>password1  
>test		
   

### Using parts and sessions

		
