# metacrack

Metacrack is a commandline tool for security researchers to create targeted hash / word association lists to help crack salted hashes in https://github.com/hashcat/hashcat. Metacrack is currently in beta, missing features and is not optimised for performance.

Each function is written as a plugin which maps directly to a command line verb for example:

`meta export values.txt output.txt` 

- `meta` is the name of the compiled command line executeable
- `export` is the verb you wish to use 
- `values.txt` and `output.txt` are parameters specific to the `export` verb.

Help for each verb can be viewed by typing `meta <plugin>` - where <plugin> is the name of the verb / plugin you wish to use - without parameters. Is is also possible to write your own plugin.

## Plugins 

