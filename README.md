# *metacrack*
Metacrack is a command line tool for security researchers and pentesters to create targeted hash / word association lists to help crack salted hashes in [hashcat](https://github.com/hashcat/hashcat). Metacrack is currently in beta, and is not yet optimised for performance. Users of Metacrack should be very comfortable with using command line tools, including Hashcat.

Each function is written as a plugin which maps directly to a command line verb, for example:

`meta export values.txt output.txt` 

- `meta` is the name of the compiled command line executeable
- `export` is the verb you wish to use 
- `values.txt` and `output.txt` are parameters specific to the `export` verb.

Help for each verb can be viewed by typing `meta <plugin> --help`. It is also possible to write your own plugin, which will automatically be loaded and executed by the `meta` command line tool. See [creating your own plugin](#creating-your-own-plugin) for more details.
  
## License
Metacrack is licensed under the MIT license. Refer to [license.txt](https://github.com/metacrackorg/metacrack/blob/main/LICENSE) for more information.

  
## Usage and features
Metacrack uses metadata associated with a hash to create a per-hash list of possible words to use where a hash is difficult or expensive to crack. Common sources of data include previous password breaches, usernames, birthdates, and emails addresses. 

Metadata is first cataloged using the *catalog* verb which stores the data in a database file, indexed by a key derived from an anonymised version of an email address.
  
By providing a file containing a list of *email:hash* or *username:hash* combinations, it is possible to perform a *lookup* to create two files that are compatible with [Hashcat](https://github.com/hashcat/hashcat) associative attack (attack mode 9). Words that would be duplicated by a rule in hashcat can be filtered out by supplying the rule to metacrack. Hashes can be filtered by Hashcat mode and iteration count to ensure that they are valid, as any inconsistencies will prevent hashcat from running the attack. Output in the form of cracked hashes from hashcat can be provided to metacrack to be removed from hash / wordlists, and exported using the *export* verb, in various formats.  
 
  > **Note**<br>
  > Ensure that you are both legally and ethically allowed to use the meta data associated with the hashes you are trying to crack. 

## Plugins 
The following plugins are documented in the order that they might be used when trying to crack a list of hashes. 
 
| Verb | Description |
| --- | --- |
| [catalog](CATALOG.md) | Add contents of files to a database.|
| [lookup](LOOKUP.md) | Lookup meta data for a list of *email:hash* or *username:hash* pairs. |
 

 
 ## Creating your own plugin
 Content to be added.
