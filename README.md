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
List of persisted devices:
![Peristence](/asserts/persistence.PNG "Peristence")

Numeric:
![Edit Device Peristence](/asserts/editdevicepersistence.PNG "Edit Device Peristence") 

Non-Numeric:
![Edit Device Non-Numeric Peristence](/asserts/editdevicepersistencenonnumeric.PNG "Edit Device Non-Numeric Peristence")

You can view history by using predefined commands or writing your own queries.
![Device History](/asserts/history.PNG "Device History") 

You can view history as basic charts too
![Device History Chart](/asserts/historychart.PNG "Device History Chart") 

Device Tab
------------
If device is persisted, then a persistence tab will show up in device details.

Numeric:
![Device Tab Table](/asserts/tabtable.PNG "Device Tab Table") 

Non-Numeric:
![Device Tab Table](/asserts/tabtablenonnumeric.PNG "Device Tab Table") 

Basic Charts:
![Device Tab Chart](/asserts/tabchart.PNG "Device Tab Chart") 

Device Import
------------
You can configure plugin to import values from Influx Db periodically in settings.
![Device Import](/asserts/deviceimport.PNG "Device Import") 

Device Import Settings 
![Device Import Settings](/asserts/deviceimportdetails.PNG "Device Import Settings") 

Imported Values as Virtual Devices
![Device Import Devices](/asserts/virtualdevices.PNG "Device Import Devices") 

Build State
-----------
[![Build State](https://ci.appveyor.com/api/projects/status/github/dk307/HSPI_InfluxDBPersistence?branch=master&svg=true)](https://ci.appveyor.com/project/dk307/HSPI-InfluxDBPersistence/build/artifacts?branch=master)
