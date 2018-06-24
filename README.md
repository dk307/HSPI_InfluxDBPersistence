Homeseer InfluxDB Persistence PlugIn
=====================================
HomeSeer plugin to save and import data from InfluxDB.  

Needs dot.net  4.7.2.

Compatibility
------------
Tested on the following platforms:
* Windows 10

Functionality
------------
* Store values from the Homeseer devices in InfluxDB on change.
* Displays the history as Tab in Device Properties 
* Basic Graph to show the history.
* Can import values from InfluxDB periodically based on query.


DB Setting
------------
![DB Settings](/asserts/dbsettings.PNG "DB Settings")

Persistence Settings
------------
![Peristence](/asserts/persistence.PNG "Peristence")

![Edit Device Peristence](/asserts/editdevicepersistence.PNG "Edit Device Peristence") 

![Edit Device Non-Numeric Peristence](/asserts/editdevicepersistencenonnumeric.PNG "Edit Device Non-Numeric Peristence")

![Device History](/asserts/history.PNG "Device History") 
You can view history by using predefined commands or writing your own queries.

![Device History Chart](/asserts/historychart.PNG "Device History Chart") 
You can view history as basic charts too

Device Tab
------------
If device is persistenced, then a persistence tab will show up in device details.

![Device Tab Table](/asserts/tabtable.PNG "Device Tab Table") 
![Device Tab Table](/asserts/tabtablenonnumeric.PNG "Device Tab Table") 




Build State
-----------
[![Build State](https://ci.appveyor.com/api/projects/status/github/dk307/HSPI_InfluxDBPersistence?branch=master&svg=true)](https://ci.appveyor.com/project/dk307/HSPI-InfluxDBPersistence/build/artifacts?branch=master)
