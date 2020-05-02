var activeMediaObj = document.getElementById("video-content");
var videoPlayer = document.getElementById("video-content");
var audioPlayer = document.getElementById("audio-content");
var currentMediaUuid = null;
var duration = 0;
var isPlaying = false;
var lastSeekPosition = 0;
var isBuffering = false;
var queue = [];
var subtitleFonts = [];
var subtitles = [];
var subtitleInstance = null;

//Get url parameter function
$.urlParam = function (name) {
    var results = new RegExp('[\?&]' + name + '=([^&#]*)').exec(window.location.href);
    return results[1] || 0;
}

//Sanitise text function
function SanitiseText(textToSanitise) {
    return textToSanitise.replace(/<script[^>]*?>.*?<\/script>/gi, '').
        replace(/<[\/\!]*?[^<>]*?>/gi, '').
        replace(/<style[^>]*?>.*?<\/style>/gi, '').
        replace(/<![\s\S]*?--[ \t\n\r]*>/gi, '').
        replace(/&nbsp;/g, '');
}

//Get UUID
var serverUuid = parseInt($.urlParam("server"));

//Create SignalR list to get password info and name
var listConnection = new signalR.HubConnectionBuilder().withUrl("/serverlistHub").build();
listConnection.on("ListUpdated", function (message) {
    var data = JSON.parse(message);

    //Find out info from the server we want
    data.Servers.forEach(function (server) {
        if (parseInt(server.ServerUuid) == serverUuid) {
            //Set name
            $("#currently-connected-to").html("Currently connected to <b>" + SanitiseText(server.Name) + "</b>");
            document.title = SanitiseText(server.Name)

            //Set has password
            if (server.HasPassword == false) {
                $("#password-entry").addClass("hidden");
            }

            //Unhide login form
            $("#login-container").removeClass("hidden");
        }
    });

    //Disconnect
    listConnection.stop();
});
listConnection.start();

//Create connect button
$("#connect-button").button();

//Create SignalR connection
var connection = new signalR.HubConnectionBuilder().withUrl("/serverHub").build();

//Handle login errors
function showError(errorMessage) {
    //Set error box message
    $("#login-response").text(errorMessage);

    //Show error box
    $("#login-response-container").removeClass("hidden");
}

connection.on("LoginError", function (error) { showError(error); });

//Handle login success
connection.on("LoginAccept", function (errorMessage) {
    //Hide login container
    $("#login-container").addClass("hidden");

    //Show everything else
    $("#root").removeClass("hidden");

    //Start video
    $(".media-content").attr("preload", "metadata");
});

//Attach connect button event handler
$("#connect-button").click(function () {
    var username = $("#username-entry").val();
    var password = $("#password-entry").val();

    if (username.length > 32) {
        showError("Error: Username cannot be longer than 32 characters");
    }
    else {
        connection.invoke("Login", serverUuid, username, password);
    }
});



var isFullscreen = false;

var elem = document.documentElement;
function openFullscreen() {
    if (elem.requestFullscreen) {
        elem.requestFullscreen();
    } else if (elem.mozRequestFullScreen) { /* Firefox */
        elem.mozRequestFullScreen();
    } else if (elem.webkitRequestFullscreen) { /* Chrome, Safari & Opera */
        elem.webkitRequestFullscreen();
    } else if (elem.msRequestFullscreen) { /* IE/Edge */
        elem.msRequestFullscreen();
    }
}
function closeFullscreen() {
    if (document.exitFullscreen) {
        document.exitFullscreen();
    } else if (document.mozCancelFullScreen) {
        document.mozCancelFullScreen();
    } else if (document.webkitExitFullscreen) {
        document.webkitExitFullscreen();
    } else if (document.msExitFullscreen) {
        document.msExitFullscreen();
    }
}

//Update subtitles function
function UpdateSubtitles(subtitlesToChangeTo) {
    //If it's null, kill the subs
    if (subtitlesToChangeTo == null) {
        if (subtitleInstance != null) {
            subtitleInstance.dispose();
            subtitleInstance = null;
        }
    }
    //Otherwise, load new subs
    else {
        if (subtitleInstance != null) {
            subtitleInstance.dispose();
            subtitleInstance = null;
        }

        subtitleInstance = new SubtitlesOctopus({
            video: videoPlayer,
            subUrl: subtitlesToChangeTo.Url,
            fonts: subtitleFonts,
            workerUrl: "/lib/JavascriptSubtitlesOctopus/dist/subtitles-octopus-worker.js"
        });
    }
}

//Create subtitle instance


//Settings button
$("#settings-button").button();

//On click, show the settings panel
$("#settings-button").click(function () {
    $(".faded-overlay").removeClass("hidden");
    $("#settings-container").removeClass("hidden");
});

//Settings window

//Close button
$("#settings-close-button").button();
$("#settings-close-button").click(function () {
    $(".faded-overlay").addClass("hidden");
    $("#settings-container").addClass("hidden");
});

//Subtitles combobox on change
$("#subtitle-track").change(function () {
    var selectedIndex = $(this).prop("selectedIndex");

    //If it's disabled, remove the subs
    if (selectedIndex == 0) {
        UpdateSubtitles(null);
    }
    //If it's set to a sub track, create subs for it
    else {
        UpdateSubtitles(subtitles[selectedIndex - 1]);
    }
});

//Local file selection
$("#local-file").change(function () {
    var file = this.files[0];

    setLocalFile(file);
});

//Progressbar
$("#local-file-progress").progressbar();

//Local file hashing
var currentFile = null;
function setLocalFile(file) {
    currentFile = file;

    $("#local-file-progress").progressbar("option", "value", 0);

    if (file == null) {
        $("#local-file-progress").addClass("hidden");
        $("#local-file-status").text("Streaming on the fly");
        return;
    }

    //Generate sha1 hash
    $("#local-file-progress").removeClass("hidden");
    $("#local-file-status").text("Checking hash...");
    var sha1 = CryptoJS.algo.SHA1.create();
    var currentOffset = 0;
    lastOffset = 0;

    loading(file,
        function (data) {
            var wordBuffer = CryptoJS.lib.WordArray.create(data);
            sha1.update(wordBuffer);
            currentOffset += data.byteLength;
            var percentage = ((currentOffset / file.size) * 100);
            console.log(percentage.toFixed(0) + '%');
            $("#local-file-progress").progressbar("option", "value", percentage);
        }, function (data) {
            console.log('100%');
            $("#local-file-progress").progressbar("option", "value", 100);
            var encrypted = sha1.finalize().toString();
            console.log('encrypted: ' + encrypted);
            $("#local-file-progress").addClass("hidden");
    });
}

function loading(file, callbackProgress, callbackFinal) {
    var chunkSize = 1024 * 1024 * 1; //Buffer is 1mb
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
    //If this file is not the current file, cancel
    if (currentFile != file) return;

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

//Volume slider
$("#volume-slider").slider({
    min: 0,
    max: 100,
    value: 100,
    range: "min",
    orientation: "vertical",
    slide: function (event, ui) {
        //Set the volume of the active media player
        activeMediaObj.volume = ui.value / 100;
    }
});

$("#volume").hover(function () { $("#volume-slider-container").removeClass("hidden"); }, function () { $("#volume-slider-container").addClass("hidden"); });

//Create playpause button
$("#playpause-button").button();

//When playpause button is clicked, send a playback state update
$("#playpause-button").click(function () {
    //If it's the buffer button, always send pause
    if ($("#playpause-button").hasClass("buffer-button")) {
        connection.invoke("UpdatePlaybackState", serverUuid, 0, "pause");
    }
    //Otherwise, it depends
    else {
        //If it's the play button, send play
        if ($("#playpause-button").hasClass("play-button")) {
            connection.invoke("UpdatePlaybackState", serverUuid, 0, "play");
        }
        //If it's the pause button, send pause
        else if ($("#playpause-button").hasClass("pause-button")) {
            connection.invoke("UpdatePlaybackState", serverUuid, 0, "pause");
        }
    }
});

//Create skip button
$("#skip-button").button();

//When skip button pressed, send a queue update if something is playing
$("#skip-button").click(function () {
    if (currentMediaUuid != 2147483647 && currentMediaUuid != null) {
        var newQueue = [...queue];
        connection.invoke("ModifyQueue", serverUuid, newQueue, true);
    }
})

//Create fullscreen button
$("#fullscreen-button").button();
$("#fullscreen-button").click(function () {
    //This is before isFullscreen is changed, so we do the opposite
    if (!isFullscreen) {
        openFullscreen();
    }
    else {
        closeFullscreen();
    }
})
document.addEventListener("fullscreenchange", function () {
    isFullscreen = !isFullscreen;

    if (isFullscreen) {
        $("#video-content").addClass("fullscreen");
        $("#controls-container").addClass("fullscreen-controls");
        $(".libassjs-canvas-parent").addClass("fullscreen-subtitles");
        $("#fullscreen-button").removeClass("expand-button");
        $("#fullscreen-button").addClass("compress-button");

        //Hide stuff
        $("#controls-container").addClass("hidden");
        $("#header").addClass("hidden");
        $("#video-top-bar").addClass("hidden");
        $("#userlist-container").addClass("hidden");
        $("#queue-files-container").addClass("hidden");

        clearInterval(resizeInterval);
    }
    else {
        $("#video-content").removeClass("fullscreen");
        $("#controls-container").removeClass("fullscreen-controls");
        $(".libassjs-canvas-parent").removeClass("fullscreen-subtitles");
        $("#fullscreen-button").removeClass("compress-button");
        $("#fullscreen-button").addClass("expand-button");

        //Unhide stuff
        $("#controls-container").removeClass("hidden");
        $("#header").removeClass("hidden");
        $("#video-top-bar").removeClass("hidden");
        $("#userlist-container").removeClass("hidden");
        $("#queue-files-container").removeClass("hidden");

        resizeInterval = setInterval(recalculateHeight, 100);
    }
});

//Get timestamp string
function GetTimestampString(secondsValue) {
    var hour = Math.floor(secondsValue / 3600);
    var minute = Math.floor((secondsValue - (hour * 3600)) / 60);
    var second = Math.floor(secondsValue - (hour * 3600) - (minute * 60));

    if (hour > 0) {
        return hour.toString() + ":" + minute.toString().padStart(2, '0') + ":" + second.toString().padStart(2, '0');
    }
    else {
        return minute.toString() + ":" + second.toString().padStart(2, '0');
    }
}

//Position slider
$("#position-slider").slider({
    min: 0,
    max: 100,
    value: 0,
    range: "min"
});

var positionSliderTimeout = null;
function SetTimestamp() {
    var currentTimestamp = GetTimestampString($("#position-slider").slider("option", "value"));
    var durationTimestamp = GetTimestampString(duration);

    $("#timestamp > p").text(currentTimestamp + " / " + durationTimestamp);
}

function UpdatePositionSlider() {
    var currentSecond = Math.floor(activeMediaObj.currentTime);
    
    //Set the position bar value to the current second
    $("#position-slider").slider("option", "value", currentSecond);

    //Set the timestamp
    SetTimestamp();

    //If we're not playing, clear the timeout
    if (activeMediaObj.paused) {
        clearTimeout(positionSliderTimeout);
    }
    //If we are playing, set a new timeout
    else {
        var timeUntilNextSecond = 1 - (activeMediaObj.currentTime - currentSecond);
        
        positionSliderTimeout = setTimeout(UpdatePositionSlider, timeUntilNextSecond * 1000);
    }
}

//When user starts moving slider, stop the slider updates
$("#position-slider").on("slidestart", function (event, ui) {
    clearTimeout(positionSliderTimeout);
});

//When user stops moving slider, send a position update
$("#position-slider").on("slidestop", function (event, ui) {
    connection.invoke("UpdatePlaybackState", serverUuid, 1, ui.value.toString());
});

//When the user moves the slider, set the timestamp
$("#position-slider").on("slide", function (event, ui) {
    SetTimestamp();
});

//Add fullscreen mouse move event
var fullscreenTimer = null;
var isMouseOverControls = false;

function refreshFullscreenControls() {
    //Check that we're in fullscreen
    if (!isFullscreen) {
        return;
    }

    //Reset the timer
    if (fullscreenTimer != null) {
        clearTimeout(fullscreenTimer);
        fullscreenTimer = null;
    }

    //Create a new timeout that triggers 
    fullscreenTimer = setTimeout(function () {
        //Check that we're still in fullscreen
        if (!isFullscreen) {
            return;
        }

        //Check that the mouse isn't hovering over it
        if (isMouseOverControls) {
            return;
        }

        fullscreenTimer = null;

        //Add the hidden class
        $("#controls-container").addClass("hidden");

    }, 2000);

    $("#controls-container").removeClass("hidden");
}

$(document).mousemove(refreshFullscreenControls);
$("#controls-container").hover(function () { isMouseOverControls = true; }, function () { isMouseOverControls = false; });

//Main container resizer
var lastHeight = 0;
function recalculateHeight() {
    //Clear if we're in mobile mode
    if (innerWidth < 768) {
        if ($("#main-container").attr("style") != "") {
            lastHeight = 0;
            $("#main-container").removeAttr("style");
        }
    }
    //Set if we're in desktop mode
    else {
        var currentHeight = $("#content-container").height();
        if (currentHeight != lastHeight) {
            $("#main-container").height($("#content-container").height());
            lastHeight = currentHeight;
        }
    }
}
var resizeInterval = setInterval(recalculateHeight, 100);



//State updates
connection.on("CurrentStateUpdated", function (jsonData) {
    var data = JSON.parse(jsonData);

    //Clear all the users
    $("#userlist").children().remove();

    //Add all the users
    data.Users.forEach(function (user) {
        //Add a new element
        $("#userlist").append($("<p class=\"user\"></p>").text(user.Username));

        //Add the uuid
        $(".user:last-child").attr("uuid", user.Uuid);

        //Change the colour if needed
        if (user.BufferState != 0) {
            $(".user:last-child").addClass("user-buffer");

            //Also add nodata if the user either has nothing or hasn't started
            if (user.BufferState > 1) {
                $(".user:last-child").addClass("user-nodata");
            }
        }
    });

    //Update the fonts
    subtitleFonts = data.SubtitleFonts;

    //Update the media information. If anything changes, we need to send a user state update
    var hasAnythingChanged = false;

    //If the currently playing media has changed
    if (currentMediaUuid != data.CurrentMediaUuid) {
        hasAnythingChanged = true;

        //If audio path is empty, set active element to video
        if (data.CurrentAudioPath == "") {
            ClearMediaEventHandlers();
            activeMediaObj = document.getElementById("video-content");
            SetMediaEventHandlers();
            $("#video-content").removeClass("hidden");
            $("#audio-content").addClass("hidden");

            //Set MIME type
            if (data.CurrentVideoPath.endsWith(".webm")) {
                $("#video-content").attr("type", "video/webm");
            }
            else if (data.CurrentVideoPath.endsWith(".mp4")) {
                $("#video-content").attr("type", "video/mp4");
            }
            //Default to video
            else {
                $("#video-content").attr("type", "video");
            }
        }
        else {
            ClearMediaEventHandlers();
            activeMediaObj = document.getElementById("audio-content");
            SetMediaEventHandlers();
            $("#audio-content").removeClass("hidden");
            $("#video-content").addClass("hidden");

            //Set MIME type
            if (data.CurrentAudioPath.endsWith(".webm")) {
                $("#audio-content").attr("type", "audio/webm");
            }
            else if (data.CurrentAudioPath.endsWith(".mp3")) {
                $("#audio-content").attr("type", "audio/mpeg");
            }
            else if (data.CurrentAudioPath.endsWith(".flac")) {
                $("#audio-content").attr("type", "audio/flac");
            }
            else if (data.CurrentAudioPath.endsWith(".ogg")) {
                $("#audio-content").attr("type", "audio/ogg");
            }
            else if (data.CurrentAudioPath.endsWith(".wav")) {
                $("#audio-content").attr("type", "audio/wave");
            }
            //Default to audio
            else {
                $("#video-content").attr("type", "audio");
            }
        }

        //Set video and audio path
        $("#video-content").attr("src", data.CurrentVideoPath);
        $("#audio-content").attr("src", data.CurrentAudioPath);

        //Set volume
        activeMediaObj.volume = $("#volume-slider").slider("option", "value") / 100;

        //Set title
        $("#video-title").text(data.MediaTitle);

        //Set duration
        $("#position-slider").slider("option", "max", Math.round(data.Duration));

        //Load the media
        activeMediaObj.load();
        console.log("Reloading video");

        //Overwrite previous variables
        currentMediaUuid = data.CurrentMediaUuid;
        duration = data.Duration;
        isPlaying = false;
        lastSeekPosition = 0;
        $("#playpause-button").removeClass("pause-button");
        $("#playpause-button").addClass("play-button");
        UpdatePositionSlider();

        //Update the seek bar
        UpdatePositionSlider();
    }

    //If the playpause state has changed
    if (isPlaying != data.IsPlaying) {
        hasAnythingChanged = true;

        //Play or pause the media player, and set the image of the button
        if (data.IsPlaying) {
            activeMediaObj.play();
            $("#playpause-button").removeClass("play-button");
            $("#playpause-button").addClass("pause-button");
        }
        else {
            activeMediaObj.pause();
            $("#playpause-button").removeClass("pause-button");
            $("#playpause-button").addClass("play-button");
        }

        //Overwrite previous variables
        isPlaying = data.IsPlaying;

        //Update the seek bar
        UpdatePositionSlider();
    }

    //If the seek position has changed
    if (lastSeekPosition != data.LastSeekPosition) {
        hasAnythingChanged = true;

        //Move the position of the media player
        activeMediaObj.currentTime = data.LastSeekPosition;

        //Overwrite previous variables
        lastSeekPosition = data.LastSeekPosition;

        //Update the seek bar
        UpdatePositionSlider();
    }

    //If the available subtitles has changed
    var hasSubtitlesChanged = true;
    if (subtitles.length == data.Subtitles.length) {
        hasSubtitlesChanged = false;
        for (i = 0; i < subtitles.length; i++) {
            if (subtitles[i].Url != data.Subtitles[i].Url) {
                hasSubtitlesChanged = true;
            }
        }
    }
    if (hasSubtitlesChanged) {
        hasAnythingChanged = true;

        //Change the values
        $("#subtitle-track > option:not(:first-child)").remove();
        for (i = 0; i < data.Subtitles.length; i++) {
            $("#subtitle-track").append(new Option(data.Subtitles[i].Name));
        }

        //Overwrite previous variables
        subtitles = data.Subtitles;

        //Kill the subs
        UpdateSubtitles(null);
    }

    //If the buffer state has changed
    if (isBuffering != data.IsBuffering) {
        //If we are currently buffering, add the buffering class to the playpause button
        if (data.IsBuffering) {
            $("#playpause-button").addClass("buffer-button");
        }
        else {
            $("#playpause-button").removeClass("buffer-button");
        }

        //Overwrite previous variables
        isBuffering = data.IsBuffering;
    }

    //If anything changed, send a user update
    if (hasAnythingChanged) {
        SendUserStateUpdate();
    }
});


//Sends a user update
function SendUserStateUpdate() {
    var bufferState;
    //Say we're ready if we're playing nothing
    if (currentMediaUuid == 2147483647) {
        bufferState = 0;
    }
    else {
        if (activeMediaObj.readyState == 0) {
            bufferState = 2;
        }
        else if (activeMediaObj.readyState >= 1 && activeMediaObj.readyState <= 3) {
            bufferState = 1;
        }
        else {
            bufferState = 0;
        }
    }
    
    var tempCurrentMedia = currentMediaUuid;
    if (tempCurrentMedia == null) {
        tempCurrentMedia = 2147483647;
    }
    connection.invoke("UpdateUserState", serverUuid, tempCurrentMedia, isPlaying, lastSeekPosition, bufferState);
}


//Video loading
function BufferStateChanged() {
    SendUserStateUpdate();
}
function SetMediaEventHandlers() {
    activeMediaObj.onloadstart = BufferStateChanged;
    activeMediaObj.onloadedmetadata = BufferStateChanged;
    activeMediaObj.oncanplay = BufferStateChanged();
    activeMediaObj.oncanplaythrough = BufferStateChanged;
    activeMediaObj.onwaiting = BufferStateChanged;
}
function ClearMediaEventHandlers() {
    activeMediaObj.onloadstart = null;
    activeMediaObj.onloadedmetadata = null;
    activeMediaObj.oncanplay = null;
    activeMediaObj.oncanplaythrough = null;
    activeMediaObj.onwaiting = null;
}

//Queue entry button
$(".queue-entry-button").button();

//Queue updates
connection.on("QueueUpdated", function (jsonData) {
    var data = JSON.parse(jsonData);

    //Set queue variable
    queue = [];

    //Clear out the current queue
    $(".queue-entry:not(:first-child)").remove();

    //Add the queue elements back in
    data.QueueItems.forEach(function (queueItem) {
        //Add to queue array
        queue.push(queueItem.Uuid);

        var queueObject = $(".queue-entry:first-child").clone();
        queueObject.appendTo("#queue");

        //Set UUID
        queueObject.attr("uuid", queueItem.Uuid);

        //Set the text on it
        if (queueItem.IsAvailable) {
            queueObject.find(".queue-entry-title").text(queueItem.Title);
        }
        else {
            queueObject.find(".queue-entry-title").html("<span class=\"download-tag\">[DOWNLOADING] </span>").append(queueItem.Title);
        }
        queueObject.find(".queue-entry-duration").text(GetTimestampString(queueItem.Duration));

        //Set button event handlers

        //If it's downloading, remove the play button. Otherwise, add an event handler for it
        if (!queueItem.IsAvailable) {
            queueObject.find(".play-now.queue-entry-button").remove();
        }
        else {
            queueObject.find(".play-now.queue-entry-button").click(function () {
                var queueIndex = $(".queue-entry:not(:first-child)").index($(this).closest(".queue-entry"));
                var queueUuid = queue[queueIndex];
                var newQueue = [...queue];
                newQueue.splice(queueIndex, 1); //Remove from old position
                newQueue.unshift(queueUuid); //Add to front
                connection.invoke("ModifyQueue", serverUuid, newQueue, true);
            });
        }
        
        queueObject.find(".move-top.queue-entry-button").click(function () {
            var queueIndex = $(".queue-entry:not(:first-child)").index($(this).closest(".queue-entry"));
            var queueUuid = queue[queueIndex];
            var newQueue = [...queue];
            newQueue.splice(queueIndex, 1); //Remove from old position
            newQueue.unshift(queueUuid); //Add to front
            if (currentMediaUuid != 2147483647 && currentMediaUuid != null) {
                newQueue.unshift(currentMediaUuid); //Add current media to front if it's not nothing
                connection.invoke("ModifyQueue", serverUuid, newQueue, false);
            }
            else {
                connection.invoke("ModifyQueue", serverUuid, newQueue, true);
            }
        });
        queueObject.find(".move-up.queue-entry-button").click(function () {
            var queueIndex = $(".queue-entry:not(:first-child)").index($(this).closest(".queue-entry"));
            var queueUuid = queue[queueIndex];
            var newQueue = [...queue];
            newQueue.splice(queueIndex, 1); //Remove from old position
            newQueue.splice(queueIndex - 1, 0, queueUuid); //Add to one position above
            if (currentMediaUuid != 2147483647 && currentMediaUuid != null) {
                newQueue.unshift(currentMediaUuid); //Add current media to front if it's not nothing
                connection.invoke("ModifyQueue", serverUuid, newQueue, false);
            }
            else {
                connection.invoke("ModifyQueue", serverUuid, newQueue, true);
            }
        });
        queueObject.find(".move-down.queue-entry-button").click(function () {
            var queueIndex = $(".queue-entry:not(:first-child)").index($(this).closest(".queue-entry"));
            var queueUuid = queue[queueIndex];
            var newQueue = [...queue];
            newQueue.splice(queueIndex, 1); //Remove from old position
            newQueue.splice(queueIndex + 1, 0, queueUuid); //Add to one position below
            if (currentMediaUuid != 2147483647 && currentMediaUuid != null) {
                newQueue.unshift(currentMediaUuid); //Add current media to front if it's not nothing
                connection.invoke("ModifyQueue", serverUuid, newQueue, false);
            }
            else {
                connection.invoke("ModifyQueue", serverUuid, newQueue, true);
            }
        });
        queueObject.find(".move-bottom.queue-entry-button").click(function () {
            var queueIndex = $(".queue-entry:not(:first-child)").index($(this).closest(".queue-entry"));
            var queueUuid = queue[queueIndex];
            var newQueue = [...queue];
            newQueue.splice(queueIndex, 1); //Remove from old position
            newQueue.push(queueUuid); //Add to back
            if (currentMediaUuid != 2147483647 && currentMediaUuid != null) {
                newQueue.unshift(currentMediaUuid); //Add current media to front if it's not nothing
                connection.invoke("ModifyQueue", serverUuid, newQueue, false);
            }
            else {
                connection.invoke("ModifyQueue", serverUuid, newQueue, true);
            }
        });
        queueObject.find(".delete.queue-entry-button").click(function () {
            var queueIndex = $(".queue-entry:not(:first-child)").index($(this).closest(".queue-entry"));
            var queueUuid = queue[queueIndex];
            var newQueue = [...queue];
            newQueue.splice(queueIndex, 1); //Remove from old position
            if (currentMediaUuid != 2147483647 && currentMediaUuid != null) {
                newQueue.unshift(currentMediaUuid); //Add current media to front if it's not nothing
                connection.invoke("ModifyQueue", serverUuid, newQueue, false);
            }
            else {
                connection.invoke("ModifyQueue", serverUuid, newQueue, true);
            }
        });
    });
});

//Add to queue button on files
$(".file-add-queue-button").button();

//File updates
connection.on("FilesUpdated", function (jsonData) {
    var data = JSON.parse(jsonData);

    //Clear out the current files
    $(".file-entry:not(:first-child)").remove();

    //Add the queue elements back in
    data.OfflineItems.forEach(function (fileItem) {
        var fileObject = $(".file-entry:first-child").clone();
        fileObject.appendTo("#files");

        //Set UUID
        fileObject.attr("uuid", fileItem.Uuid);

        //Set the text on it
        fileObject.find(".file-entry-title").text(fileItem.Title);
        fileObject.find(".file-entry-duration").text(GetTimestampString(fileItem.Duration));

        //Add button event handler
        fileObject.find(".file-add-queue-button").click(function () {
            var thisUuid = parseInt($(this).closest(".file-entry").attr("uuid"));
            var newQueue = [...queue];

            //Add the UUID to the end of the queue
            newQueue.push(thisUuid);

            //Send modify queue
            if (currentMediaUuid != 2147483647 && currentMediaUuid != null) {
                newQueue.unshift(currentMediaUuid); //Add current media to front if it's not nothing
                connection.invoke("ModifyQueue", serverUuid, newQueue, false);
            }
            else {
                connection.invoke("ModifyQueue", serverUuid, newQueue, true);
            }
        });
    });
});

//Download button
$("#download-button").button();

//Send download packet when download button clicked
$("#download-button").click(function () {
    var urlToDownload = $("#url-entry").val();
    //Only download if there is something in the box
    if (urlToDownload != "") {
        connection.invoke("DownloadMedia", serverUuid, urlToDownload);
    }

    //Clear the textbox
    $("#url-entry").val("");
})

//Start signalr
connection.start().then(function () {
    
}).catch(function (error) {
    showError("Failed to connect: " + error);
});

//On signalr closed, redirect
connection.on("Closed", function () {
    connection.stop();
});

connection.onclose(function () {
    window.location.href = "/";
})