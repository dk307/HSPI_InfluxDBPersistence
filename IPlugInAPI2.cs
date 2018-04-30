using HomeSeerAPI;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace Hspi
{
    /// <summary>
    /// Just a wrapper on basic HS plugin
    /// </summary>
    /// <seealso cref="IPlugInAPI" />
    internal abstract class IPlugInAPI2 : IPlugInAPI
    {
        /// <summary>
        ///     Test our SCS client connection: <see cref="Hspi" /> is connected.
        ///     The console wrapper will call this periodically to check if there is a problem.
        /// </summary>
        /// <value><c>true</c> if connected; otherwise, <c>false</c>.</value>
        public abstract bool Connected { get; }

        /// <summary>
        /// Gets the name.
        /// </summary>
        /// <value>
        /// The name.
        /// </value>
        public abstract string Name { get; }

        /// <summary>
        ///     Get the instance name of this plugin.  Only valid if SupportsMultipleInstances is true.
        ///     Multiple instance are not supported by this plugin.
        /// </summary>
        /// <returns>System.String</returns>
        public abstract string InstanceFriendlyName();

        /// <summary>
        ///     API's that this plugin supports.
        ///     This is a bit field.
        ///     All plugins must set CA_IO for I/O support.
        ///     Other values: CA_Security, CA_Thermostat, CA_Music, CA_SourceSwitch.
        /// </summary>
        /// <returns>System.Int32</returns>
        public abstract int Capabilities();

        /// <summary>
        ///     Plugin licensing mode:
        ///     1 = plugin is not licensed,
        ///     2 = plugin is licensed and user must purchase a license but there is a 30-day trial.
        /// </summary>
        /// <returns>System.Int32</returns>
        public abstract int AccessLevel();

        /// <summary>
        ///     Indicate if the plugin supports multiple instances.
        ///     The plugin may be launched multiple times and will be passed a unique instance name as a command line parameter to
        ///     the Main function.
        /// </summary>
        public abstract bool SupportsMultipleInstances();

        /// <summary> Indicate if plugin supports multiple instances using a single executable.</summary>
        public abstract bool SupportsMultipleInstancesSingleEXE();

        /// <summary>
        ///     Indicate if the plugin supports the ability to add devices through the Add Device link on the device utility page.
        ///     If <c>true</c>, a tab appears on the add device page that allows the user to configure specific options for the new
        ///     device.
        /// </summary>
        public abstract bool SupportsAddDevice();

        /// <summary>
        ///     HomeSeer may call this function at any time to get the status of the plug-in.
        ///     Normally the status is displayed on the Interfaces page.
        ///     intStatus field: OK, INFO, WARNING, CRITICAL, FATAL.
        ///     sStatus field: string.
        /// </summary>
        /// <returns>
        ///     IPlugInAPI.strInterfaceStatus
        /// </returns>
        public abstract IPlugInAPI.strInterfaceStatus InterfaceStatus();

        /// <summary>
        ///     When you wish to have HomeSeer call back in to your plug-in or application when certain events happen in the
        ///     system,
        ///     call the RegisterEventCB procedure and provide it with event you wish to monitor.
        ///     See RegisterEventCB for more information and an example and event types.
        /// </summary>
        public abstract void HSEvent(Enums.HSEvent eventType, object[] parameters);

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     This function is available for the ease of converting older HS2 plugins, however, it is desirable to use the new
        ///     clsPageBuilder class for all new development.
        ///     This function is called by HomeSeer from the form or class object that a web page was registered with using
        ///     RegisterConfigLink.
        ///     You must have a GenPage procedure per web page that you register with HomeSeer.
        ///     This page is called when the user requests the web page with an HTTP Get command, which is the default operation
        ///     when the browser requests a page.
        /// </summary>
        public abstract string GenPage(string link);

        /// <summary>
        ///     When your plugin web page has form elements on it, and the form is submitted, this procedure is called to handle
        ///     the HTTP "Put" request.
        ///     There must be one PagePut procedure in each plugin object or class that is registered as a web page in HomeSeer.
        /// </summary>
        public abstract string PagePut(string data);

        /// <summary>
        ///     Called when HomeSeer is not longer using the plugin.
        ///     This call will be made if a user disables a plugin from the interfaces configuration page and when HomeSeer is shut
        ///     down.
        /// </summary>
        public abstract void ShutdownIO();

        /// <summary>
        ///     HSTouch uses the Generic Event callback in some music plug-ins so that it can be notified of when a song changes,
        ///     rather than having to repeatedly query the music plug-in for the current song status.
        ///     If this property is present (and returns True), especially in a Music plug-in,
        ///     then HSTouch (and other plug-ins) will know that your HSEvent procedure can handle generic callbacks.
        /// </summary>
        public abstract bool RaisesGenericCallbacks();

        /// <summary>
        ///     SetIOMulti is called by HomeSeer when a device that your plugin owns is controlled.
        ///     Your plugin owns a device when it's INTERFACE property is set to the name of your plugin.
        /// </summary>
        /// <param name="colSend">
        ///     This is a collection of CAPIControl objects, one object for each device that needs to be controlled.
        ///     Look at the ControlValue property to get the value that device needs to be set to.
        /// </param>
        public abstract void SetIOMulti(List<CAPI.CAPIControl> colSend);

        /// <summary>
        ///     Initialize the plugin and associated hardware/software, start any threads
        /// </summary>
        /// <param name="port">The COM port for the plugin if required.</param>
        /// <returns>Warning message or empty for success.</returns>
        public abstract string InitIO(string port);

        /// <summary>
        ///     If a device is owned by your plug-in (interface property set to the name of the plug-in) and the device's
        ///     status_support property is set to True,
        ///     then this procedure will be called in your plug-in when the device's status is being polled, such as when the user
        ///     clicks "Poll Devices" on the device status page.
        ///     Normally your plugin will automatically keep the status of its devices updated.
        ///     There may be situations where automatically updating devices is not possible or CPU intensive.
        ///     In these cases the plug-in may not keep the devices updated. HomeSeer may then call this function to force an
        ///     update of a specific device.
        ///     This request is normally done when a user displays the status page, or a script runs and needs to be sure it has
        ///     the latest status.
        /// </summary>
        /// <param name="deviceId">Reference Id for the device</param>
        /// <returns>IPlugInAPI.PollResultInfo</returns>
        public abstract IPlugInAPI.PollResultInfo PollDevice(int deviceId);

        /// <summary>
        ///     Indicate if the plugin allows for configuration of the devices via the device utility page.
        ///     This will allow you to generate some HTML controls that will be displayed to the user for modifying the device.
        /// </summary>
        public abstract bool SupportsConfigDevice();

        /// <summary> Indicate if the plugin manages all devices in the system. </summary>
        public abstract bool SupportsConfigDeviceAll();

        /// <summary>
        ///     This function is called when a user posts information from your plugin tab on the device utility page.
        /// </summary>
        /// <param name="deviceId">The device reference id.</param>
        /// <param name="data">The post data.</param>
        /// <param name="user">The name of logged in user.</param>
        /// <param name="userRights">The rights of the logged in user.</param>
        /// <returns>Enums.ConfigDevicePostReturn.</returns>
        public abstract Enums.ConfigDevicePostReturn ConfigDevicePost(int deviceId,
            string data,
            string user,
            int userRights);

        /// <summary>
        ///     If SupportsConfigDevice returns <c>true</c>, this function will be called when the device properties are displayed
        ///     for your device.
        ///     The device properties is displayed from the Device Utility page.
        ///     This page displays a tab for each plugin that controls the device.
        ///     Normally, only one plugin will be associated with a single device.
        ///     If there is any configuration that needs to be set on the device, you can return any HTML that you would like
        ///     displayed.
        ///     Normally this would be any jquery controls that allow customization of the device.
        ///     The returned HTML is just an HTML fragment and not a complete page.
        /// </summary>
        /// <param name="deviceId">The device reference id.</param>
        /// <param name="user">The name of logged in user.</param>
        /// <param name="userRights">The rights of the logged in user.</param>
        /// <param name="newDevice">
        ///     <c>True</c> if this is a new device being created for the first time.
        ///     In this case, the device configuration dialog may present different information than when simply editing an
        ///     existing device.
        /// </param>
        /// <returns>A string containing HTML to be displayed. Return an empty string if there is not configuration needed.</returns>
        public abstract string ConfigDevice(int deviceId, string user, int userRights, bool newDevice);

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Called in your plugin by HomeSeer whenever the user uses the search function of HomeSeer, and your plugin is loaded
        ///     and initialized.
        ///     Unlike ActionReferencesDevice and TriggerReferencesDevice, this search is not being specific to a device,
        ///     it is meant to find a match anywhere in the resources managed by your plugin.
        ///     This could include any textual field or object name that is utilized by the plugin.
        /// </summary>
        /// <param name="searchString">The search string.</param>
        /// <param name="regEx">if set to <c>true</c> then the search string is a regular expression.</param>
        /// <returns>Array of SearchReturn items describing what was found and where it was found.</returns>
        public abstract SearchReturn[] Search(string searchString, bool regEx);

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary> Call a function in the plugin. </summary>
        /// <param name="functionName">The name of the function to call.</param>
        /// <param name="parameters">An array of parameters to pass to the function.</param>
        public abstract object PluginFunction(string functionName, object[] parameters);

        /// <summary> Get a property from the plugin. </summary>
        /// <param name="propertyName">The name of the property to access.</param>
        /// <param name="parameters">An array of parameters to pass to the function.</param>
        public abstract object PluginPropertyGet(string propertyName, object[] parameters);

        /// <summary> Set a property of the plugin. </summary>
        /// <param name="propertyName">The name of the property to access.</param>
        /// <param name="value">The value to set the property.</param>
        public abstract void PluginPropertySet(string propertyName, object value);

        /// <summary>
        ///     If your plugin is registered as a Speak proxy plugin, then when HomeSeer is asked to speak something,
        ///     it will pass the speak information to your plugin using this procedure.
        ///     When your plugin is ready to do the actual speaking, it should call SpeakProxy,
        ///     and pass the information that it got from this procedure to SpeakProxy.
        ///     It may be necessary or a feature of your plugin to modify the text being spoken or the host/instance list provided
        ///     in the host parameter - this is acceptable.
        /// </summary>
        /// <param name="deviceId">This is the device that is to be used for the speaking.</param>
        /// <param name="text">
        ///     This is the text to be spoken, or if it is a WAV file to be played, then the characters ":\" will be
        ///     found starting at position 2 of the string as playing a WAV file with the speak command in HomeSeer REQUIRES a
        ///     fully qualified path and filename of the WAV file to play.
        /// </param>
        /// <param name="wait">
        ///     This parameter tells HomeSeer whether to continue processing commands immediately or to wait until
        ///     the speak command is finished - pass this parameter unchanged to SpeakProxy.
        /// </param>
        /// <param name="host">
        ///     This is a list of host:instances to speak or play the WAV file on.  An empty string or a single
        ///     asterisk (*) indicates all connected speaker clients on all hosts.  Normally this parameter is passed to SpeakProxy
        ///     unchanged.
        /// </param>
        public abstract void SpeakIn(int deviceId, string text, bool wait, string host);

        /// <summary> The number of actions the plugin supports. </summary>
        public abstract int ActionCount();

        /// <summary>
        ///     Return TRUE if the given action is configured properly.
        ///     There may be times when a user can select invalid selections for the action and in this case you would return FALSE
        ///     so HomeSeer will not allow the action to be saved.
        /// </summary>
        /// <param name="actionInfo">Object describing the action.</param>
        public abstract bool ActionConfigured(IPlugInAPI.strTrigActInfo actionInfo);

        /// <summary>
        ///     This function is called from the HomeSeer event page when an event is in edit mode.
        ///     Your plugin needs to return HTML controls so the user can make action selections.
        ///     Normally this is one of the HomeSeer jquery controls such as a clsJquery.jqueryCheckbox.
        /// </summary>
        /// <param name="uniqueControlId">
        ///     A unique string that can be used with your HTML controls to identify the control. All controls
        ///     need to have a unique ID.
        /// </param>
        /// <param name="actionInfo">Object that contains information about the action like current selections.</param>
        /// <returns>HTML controls that need to be displayed so the user can select the action parameters.</returns>
        public abstract string ActionBuildUI(string uniqueControlId, IPlugInAPI.strTrigActInfo actionInfo);

        /// <summary>
        ///     When a user edits your event actions in the HomeSeer events, this function is called to process the selections.
        /// </summary>
        /// <param name="postData">A collection of name value pairs that include the user's selections.</param>
        /// <param name="actionInfo">Object that contains information about the action.</param>
        /// <returns>
        ///     Object that holds the parsed information for the action. HomeSeer will save this information for you in the
        ///     database.
        /// </returns>
        public abstract IPlugInAPI.strMultiReturn ActionProcessPostUI(NameValueCollection postData,
            IPlugInAPI.strTrigActInfo actionInfo);

        public abstract string ActionFormatUI(IPlugInAPI.strTrigActInfo actionInfo);

        /// <summary> Indicate if the given devices is referenced by the given action. </summary>
        public abstract bool ActionReferencesDevice(IPlugInAPI.strTrigActInfo actionInfo, int deviceId);

        /// <summary>
        ///     When an event is triggered, this function is called to carry out the selected action.
        ///     Use the ActInfo parameter to determine what action needs to be executed then execute this action.
        /// </summary>
        public abstract bool HandleAction(IPlugInAPI.strTrigActInfo actionInfo);

        /// <summary> Return the HTML controls for a given trigger. </summary>
        public abstract string TriggerBuildUI(string uniqueControlId, IPlugInAPI.strTrigActInfo triggerInfo);

        /// <summary>
        ///     Process a post from the events web page when a user modifies any of the controls related to a plugin trigger.
        ///     After processing the user selctions, create and return a strMultiReturn object.
        /// </summary>
        public abstract IPlugInAPI.strMultiReturn TriggerProcessPostUI(NameValueCollection postData,
            IPlugInAPI.strTrigActInfo actionInfo);

        /// <summary>
        ///     After the trigger has been configured, this function is called in your plugin to display the configured
        ///     trigger.
        /// </summary>
        /// <returns>Text that describes the given trigger.</returns>
        public abstract string TriggerFormatUI(IPlugInAPI.strTrigActInfo actionInfo);

        /// <summary>
        ///     Although this appears as a function that would be called to determine if a trigger is true or not, it is not.
        ///     Triggers notify HomeSeer of trigger states using TriggerFire , but Triggers can also be conditions, and that is
        ///     where this is used.
        ///     If this function is called, TrigInfo will contain the trigger information pertaining to a trigger used as a
        ///     condition.
        ///     When a user's event is triggered and it has conditions, the conditions need to be evaluated immediately,
        ///     so there is not regularity with which this function may be called in your plugin.
        ///     It may be called as often as once per second or as infrequently as once in a blue moon.
        /// </summary>
        public abstract bool TriggerTrue(IPlugInAPI.strTrigActInfo actionInfo);

        /// <summary> Indicate if the given device is referenced by the given trigger. </summary>
        public abstract bool TriggerReferencesDevice(IPlugInAPI.strTrigActInfo actionInfo, int deviceId);

        /// <summary>
        ///     A complete page needs to be created and returned.
        ///     Web pages that use the clsPageBuilder class and registered with hs.RegisterLink and hs.RegisterConfigLink will then
        ///     be called through this function.
        /// </summary>
        /// <param name="page">The name of the page as passed to the hs.RegisterLink function.</param>
        /// <param name="user">The name of logged in user.</param>
        /// <param name="userRights">The rights of the logged in user.</param>
        /// <param name="queryString">The query string.</param>
        public abstract string GetPagePlugin(string page, string user, int userRights, string queryString);

        /// <summary>
        ///     When a user clicks on any controls on one of your web pages, this function is then called with the post data. You
        ///     can then parse the data and process as needed.
        /// </summary>
        /// <param name="page">The name of the page as passed to the hs.RegisterLink function.</param>
        /// <param name="data">The post data.</param>
        /// <param name="user">The name of logged in user.</param>
        /// <param name="userRights">The rights of the logged in user.</param>
        /// <returns>Any serialized data that needs to be passed back to the web page, generated by the clsPageBuilder class.</returns>
        public abstract string PostBackProc(string page, string data, string user, int userRights);

        public bool HSCOMPort => GetHscomPort();

        /// <summary>
        ///     Return the name of the action given an action number. The name of the action will be displayed in the HomeSeer
        ///     events actions list.
        /// </summary>
        /// <param name="actionNumber">The number of the action. Each action is numbered, starting at 1.</param>
        /// <returns>Name of the action.</returns>
        public abstract string get_ActionName(int actionNumber);

        /// <summary> Indicate if the given trigger can also be used as a condition for the given grigger number. </summary>
        public abstract bool get_HasConditions(int triggerNumber);

        public abstract bool HasTriggers { get; }

        public abstract int TriggerCount { get; }

        /// <summary>
        ///     The HomeSeer events page has an option to set the editing mode to "Advanced Mode".
        ///     This is typically used to enable options that may only be of interest to advanced users or programmers.
        ///     The Set in this function is called when advanced mode is enabled.
        ///     Your plugin can also enable this mode if an advanced selection was saved and needs to be displayed.
        /// </summary>
        public bool ActionAdvancedMode { get; set; }

        /// <summary> Return the name of the given trigger based on the trigger number. </summary>
        public abstract string get_TriggerName(int triggerNumber);

        /// <summary> Return the number of sub triggers your plugin supports. </summary>
        public abstract int get_SubTriggerCount(int triggerNumber);

        /// <summary> Return the text name of the sub trigger given its trugger number and sub trigger number. </summary>
        public abstract string get_SubTriggerName(int triggerNumber, int subTriggerNumber);

        /// <summary> Indicate if the given trigger is configured properly. </summary>
        public abstract bool get_TriggerConfigured(IPlugInAPI.strTrigActInfo actionInfo);

        /// <summary>
        ///     Set to <c>true</c> if the trigger is being used as a CONDITION.
        ///     Check this value in BuildUI and other procedures to change how the trigger is rendered if it is being used as a
        ///     condition or a trigger.
        /// </summary>
        public abstract bool get_Condition(IPlugInAPI.strTrigActInfo actionInfo);

        public abstract void set_Condition(IPlugInAPI.strTrigActInfo actionInfo, bool value);

        /// <summary> Indicate if the plugin has any triggers. </summary>
        protected abstract bool GetHasTriggers();

        /// <summary> Number of triggers the plugin supports. </summary>
        protected abstract int GetTriggerCount();

        /// <summary> Indicate if Homeseer should manage a COM port for the plugin. </summary>
        /// <value><c>true</c> if COM port required; otherwise, <c>false</c>.</value>
        protected abstract bool GetHscomPort();

        /// <summary>
        ///     Sets the device value.
        /// </summary>
        /// <param name="deviceId">The device reference identifier.</param>
        /// <param name="value">The value/status of the device.</param>
        /// <param name="trigger">if set to <c>true</c> process triggers normally, otherwise only change the value.</param>
        public abstract void SetDeviceValue(int deviceId, double value, bool trigger = true);
    }
}