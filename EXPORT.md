# *export*

Takes an *email:hash* file(s) and combines it with the output file from a hashcat [hashcat](https://github.com/hashcat/hashcat) mode 9 attack, to create the following outputs.

- *name*.left.txt - The remaining *email:hash* pairs which were not found in the output file.
- *name*.found.txt - A list of *hash:plain* pairs found in both input files.
- *name*.plains.txt - A list of *email:plain* pairs created by combining the two input files

## Usage

`meta export hashespath outputpath [options]`
&nbsp;<br>
&nbsp;<br>

| Option | Description |
| :--- | :--- |
| --remove-hash | Path to the file to remove any matched hashes from Usual *name*.hash|
| --remove-word | Path to the file (usually *name*.word) to remove associated words when used in conjunction with --remove-hash. |
| --shuck| The path to the file that will be used to convert founds back to plains.  |
| --ignore-salt| Do not compare salts when matching hashes. Default is false. |
| <img width=350> | |

 
## Examples
 
In the docuemntation for (CATALOG.md) and (LOOKUP.md) we used the *breach.txt* file as a list of *email:plain* value we wanted to crack

>alice.smith<span>@icloud.com:$2a$10$XsDGiVuwaoYP8uGDoleDmuWV9s4MtMCn1OWzV3PEEFL4gtYVroNW2
>alice1974<span>@apple.com:$2a$10$myx7zGGnlbgRxyaPhF0NwuYkJuQ0qSHuShRpL8bQVfgGHQaIf4.Hy

with an output file from Hashcat containing the following *hash:plain* pairs. We can run the *export* command to associate the emails with the cracked plains, and to clean up any duplicated from hashcat. The command below creates the following outputs: 

`meta export breach.txt breach.output.txt`
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
>test2  
>test1974      
>test21974
	
  
### Using a rule to cut down on repetitions

In the previous example, the words *password1* and *password5*, and *test* and *test2* were returned as guesses for the same hash. Although this is sometimes unavoidable, it is far more efficient to use a rule inside hashcat then to specify similar guesses for a hash on seperate lines.

Re-run the command but this time specify a rule:
	
  > **Note**<br>
  > The *best64.rule* file can be found inside the /rules folder of your hashcat installation or from the GitHub repository [here](https://github.com/hashcat/hashcat/blob/master/rules/best64.rule). 

`meta lookup breach.txt metadata.db -m 3200 -r best64.rule -f p u i`
&nbsp;<br>
&nbsp;<br>
	
*breach.hash*
>$2a$10$XsDGiVuwaoYP8uGDoleDmuWV9s4MtMCn1OWzV3PEEFL4gtYVroNW2
>$2a$10$XsDGiVuwaoYP8uGDoleDmuWV9s4MtMCn1OWzV3PEEFL4gtYVroNW2
>$2a$10$XsDGiVuwaoYP8uGDoleDmuWV9s4MtMCn1OWzV3PEEFL4gtYVroNW2
>$2a$10$XsDGiVuwaoYP8uGDoleDmuWV9s4MtMCn1OWzV3PEEFL4gtYVroNW2
>$2a$10$myx7zGGnlbgRxyaPhF0NwuYkJuQ0qSHuShRpL8bQVfgGHQaIf4.Hy
>$2a$10$myx7zGGnlbgRxyaPhF0NwuYkJuQ0qSHuShRpL8bQVfgGHQaIf4.Hy

*breach.word*
>password1  
>password5  
>alice74  
>alice2  
>test  
>test21974  		

By using a rule, we have moved 25% of the guesses from the output files.
 
### Using sessions

In our previous example, current versions of Hashcat (6.2.5) would continue to generate hashes from the words supplied for all hashes, even if a match was found. Sessions allow us to try a guess per hash, stop and remove any matched hashes and corresponsing words that have a solution, and then continue. 
	
To split the *.hash* and *.word* files into multiple sessions, use the *-s* or *--sessions* option as follows:

`meta lookup breach.txt metadata.db -m 3200 -f p u i -s 3`
&nbsp;<br>
&nbsp;<br>
The following files are created
>breach.session1.hash  
>breach.session1.word  
>breach.session2.hash  
>breach.session2.word  
>breach.session3.hash  
>breach.session3.word 

To run hashcat against the first pair of files, use a command such as this:
	
`hashcat -a 9 -m 3200 breach.session1.hash breach.session1.word -o breach.session1.output.txt`
&nbsp;<br>
&nbsp;<br>
See the [export]() page for details on how to remove matched values from *.hash* and *.word* files before starting the next session.
