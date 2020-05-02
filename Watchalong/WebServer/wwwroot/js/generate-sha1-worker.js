importScripts("/lib/crypto-js/crypto-js.js");

//When we receive a message, start the hashing with the provided file
onmessage = function (e) {
    var file = e.data;
    var sha1 = CryptoJS.algo.SHA1.create();
    var currentOffset = 0;
    lastOffset = 0;

    loading(file,
        function (data) {
            var wordBuffer = CryptoJS.lib.WordArray.create(data);
            sha1.update(wordBuffer);
            currentOffset += data.byteLength;
            //Send progress update with currentOffset
            postMessage(["progress", file, currentOffset])
        }, function (data) {
            var encrypted = sha1.finalize().toString();
            //Send complete with hash
            postMessage(["complete", file, encrypted])
    });
}

function loading(file, callbackProgress, callbackFinal) {
    var chunkSize = 1024 * 1024 * 8; //Buffer is 8mb
    var offset = 0;
    var size = chunkSize;
    var partial;
    var index = 0;

    if (file.size === 0) {
        callbackFinal();
    }
    while (offset < file.size) {
        partial = file.slice(offset, offset + size);
        var reader = new FileReader;
        reader.size = chunkSize;
        reader.offset = offset;
        reader.index = index;
        reader.onload = function (evt) {
            callbackRead(this, file, evt, callbackProgress, callbackFinal);
        };
        reader.readAsArrayBuffer(partial);
        offset += chunkSize;
        index += 1;
    }
}

var lastOffset;
function callbackRead(reader, file, evt, callbackProgress, callbackFinal) {
    if (lastOffset === reader.offset) {
        // in order chunk
        lastOffset = reader.offset + reader.size;
        callbackProgress(evt.target.result);
        if (reader.offset + reader.size >= file.size) {
            callbackFinal();
        }
    } else {
        // not in order chunk
        timeout = setTimeout(function () {
            callbackRead(reader, file, evt, callbackProgress, callbackFinal);
        }, 10);
    }
}