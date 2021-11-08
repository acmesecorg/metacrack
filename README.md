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
Metacrack is licensed under the MIT license. Refer to license.txt for more information.

## Plugins 

The following plugins are documented in the order that they might be used to create a file containing a list of hashes and associated words, to be used with [Hashcat](https://github.com/hashcat/hashcat) attack mode 9.
  
 
