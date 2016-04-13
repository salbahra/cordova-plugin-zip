var cordova = require('cordova'),
    ToUpperPlugin= require('./Zip');

module.exports = {

    unzip: function (successCallback, errorCallback, strInput) {
        var doesitwork = Unzipper.Unzipper.unzip(strInput[1], strInput[0]).then(
            function (result) {
                console.log('complete');
                successCallback('success :-)');
            },
            function (err) {
                console.log('error');
                errorCallback('error :-(');
            },
            function (prog) {
                console.log('PROG{ loaded: ' + prog.loaded + ' total: ' + prog.total + '}');
                successCallback({ loaded: prog.loaded, total: prog.total }, { keepCallback: true});
            }
            );
    }
};
require("cordova/exec/proxy").add("Zip", module.exports);