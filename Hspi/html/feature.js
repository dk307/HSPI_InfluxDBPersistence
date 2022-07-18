function getUrlParameterOrEmpty(key) {
	return getUrlParameter(key) ?? '';
};

function getUrlParameter(sParam) {
    var sPageURL = window.location.search.substring(1),
        sURLVariables = sPageURL.split('&'),
        sParameterName,
        i;

    for (i = 0; i < sURLVariables.length; i++) {
        sParameterName = sURLVariables[i].split('=');

        if (sParameterName[0] === sParam) {
            return sParameterName[1] === undefined ? true : decodeURIComponent(sParameterName[1]);
        }
    }
};

function setupIFrame(ref, framePage) {	
	// hide save button
	// $('#save_device_config').hide();
	
	var params = {
        refId   : ref,
        feature : getUrlParameterOrEmpty('feature')
    };
	
	var iFrameUrl = '/InfluxDBPersistence/' + framePage + '?' + $.param(params);
	
	$('#InfluxDBPersistence-md>#plugintabhtml')
		.html("<iframe id=\"influxdbpersistenceiframe\" src=" + iFrameUrl +' scrolling="no" style="width: 1px;min-width: 100%;border: none; width: 100%; height: 18rem"></iframe>');
	 		
	iFrameResize({});
};





