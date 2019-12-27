# ItemDropLogger
The Revival of Player Item History after the latest was removed   
By IcyPheonix, remade by Hiarni, updated by KingArty, updated again by Quinci
#### Updated to api 2.1 by Quinci .  
Added command /li to list all ignored items <br>
Default config file does not automatically generate with any data, use the one provided. Streamreader appends to a new Config object that already has values, making them unchangeable via config. Don't know enough csharp or json to fix, so I just set list to empty and included a config file.
#### Commands & Permissions .  
`droplog.search`  lr - Check what items the target player has received.  
`droplog.search`  lg - Check what items the target player has given.    
`droplog.reload`  lreload - Reloads the itemdroplog configuration file.   
`droplog.flush`  lflush - Clears the itemdrop logs.   
`droplog.list`  li - Lists items that do not get logged.
