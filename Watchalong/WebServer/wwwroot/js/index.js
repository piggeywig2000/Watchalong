$(".server-join").button();

var connection = new signalR.HubConnectionBuilder().withUrl("/serverlistHub").build();

connection.on("ListUpdated", function (message) {
    var data = JSON.parse(message);
    UpdateAvailableServers(data.Servers);
});

function UpdateAvailableServers(listOfServers) {
    //Set the server count
    $("#server-count").text("Server count: " + listOfServers.length);

    //Remove all servers except the first, which is hidden
    $(".server:not(.server:first-child)").remove();

    //Add all the servers
    listOfServers.forEach(function (server) {
        //Clone the server object
        $("#servers-container").children().first().clone().appendTo("#servers-container");

        //Get the new server object
        var serverObj = $("#servers-container").children().last();

        //Set UUID
        serverObj.attr("uuid", server.ServerUuid);

        //Set name
        serverObj.find(".server-name").text(server.Name);

        //Set image
        if (server.ImageUrl != "") {
            serverObj.find(".server-image").attr("src", server.ImageUrl);
        }

        //Set user count
        serverObj.find(".server-user-count").html("<i class=\"fas fa-user right-margin\"></i>In room: " + server.UserCount);

        //Set password
        if (server.HasPassword) {
            serverObj.find(".server-has-password").html("<i class=\"fas fa-lock right-margin\"></i>Has password");
        }
        else {
            serverObj.find(".server-has-password").html("<i class=\"fas fa-unlock right-margin\"></i>No password");
        }

        //Set on click for join button
        serverObj.find(".server-join").click(function () {
            window.location.href = "/Watch?server=" + server.ServerUuid;
        });
    });
}

connection.start();