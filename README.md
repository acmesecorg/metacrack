# *metacrack*

Metacrack is a commandline tool for security researchers to create targeted hash / word association lists to help crack salted hashes in https://github.com/hashcat/hashcat. Metacrack is currently in beta, missing features and is not optimised for performance.

Each function is written as a plugin which maps directly to a command line verb for example:

`meta export values.txt output.txt` 

- `meta` is the name of the compiled command line executeable
- `export` is the verb you wish to use 
- `values.txt` and `output.txt` are parameters specific to the `export` verb.

Help for each verb can be viewed by typing `meta <plugin>` - where <plugin> is the name of the verb / plugin you wish to use - without parameters. It is also possible to write your own plugin as a c# dll, which will automatically be loaded and executed by the `meta` command line tool.
  
  > *Note*
  > The creation of metacrack pre-dates the announcement of the formation of the legal entity formally known as Facebook, and the similarity in names is both unfortunate and purely coincidental.
  
## License
Metacrack is licensed under the MIT license. Refer to [license.txt](https://github.com/metacrackorg/metacrack/blob/main/LICENSE) for more information.
  
## Usage

Metacrack uses metadata associated with a hash to create a per-hash list of possible words to use where a hash is difficult or expensive to crack. Common sources of data include previous password breaches, usernames, birthdates, and emails addresses.
  
  > *Note*
  > Ensure that you are both legally and ethically allowed to use the meta data associated with the hashes you are trying to crack. 

## Plugins 

The following plugins are documented in the order that they might be used to create a file containing a list of hashes and associated words, to be used with [Hashcat](https://github.com/hashcat/hashcat) attack mode 9.
  
 ### *catalog*
  
 Add contents of files to a data catalog. Files should start with an identifier such as an email address and contain one or more values, seperated by the `:` character e.g.
 
 ```
 alice@acme.org:password::
 bob@acme.org:letmein::1979
 carol@acme.org:ilovecats:Carol Smith:
 ```
  
