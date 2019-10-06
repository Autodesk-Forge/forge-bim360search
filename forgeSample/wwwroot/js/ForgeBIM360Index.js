/////////////////////////////////////////////////////////////////////
// Copyright (c) Autodesk, Inc. All rights reserved
// Written by Forge Partner Development
//
// Permission to use, copy, modify, and distribute this software in
// object code form for any purpose and without fee is hereby granted,
// provided that the above copyright notice appears in all copies and
// that both that copyright notice and the limited warranty and
// restricted rights notice below appear in all supporting
// documentation.
//
// AUTODESK PROVIDES THIS PROGRAM "AS IS" AND WITH ALL FAULTS.
// AUTODESK SPECIFICALLY DISCLAIMS ANY IMPLIED WARRANTY OF
// MERCHANTABILITY OR FITNESS FOR A PARTICULAR USE.  AUTODESK, INC.
// DOES NOT WARRANT THAT THE OPERATION OF THE PROGRAM WILL BE
// UNINTERRUPTED OR ERROR FREE.
/////////////////////////////////////////////////////////////////////

$(document).ready(function () {
    $.getJSON("/api/forge/clientid", function (res) {
        $("#ClientID").val(res.id);
        $("#provisionAccountSave").click(function () {
            $('#provisionAccountModal').modal('toggle');

            getAccounts(function (hubs) {
                if (hubs.length == 1) {
                    $("#bim360accounts").val(hubs[0].id);
                }
            });
        });
    });

    $('#startIndex').click(function () {
        jQuery.ajax({
            url: '/api/forge/datamanagement/' + $('#bim360accounts').val() + '/index',
            success: function (url) {
                $('.progress').show();
                $('#indexProgressBar span').text('Searching for files...');
            }
        });
    });

    startConnection();

    $('.progress').hide();
});

var connection;
var connectionId;

var totalFiles = 0;
var totalProcessed = 0;
var done = false;

function startConnection(onReady) {
    if (connection && connection.connectionState) { if (onReady) onReady(); return; }
    connection = new signalR.HubConnectionBuilder().withUrl("/api/signalr/modelderivative").build();
    connection.start()
        .then(function () {
            connection.invoke('getConnectionId')
                .then(function (id) {
                    connectionId = id; // we'll need this...
                    if (onReady) onReady();
                });
        });

    connection.on("fileFound", function (hubId) {
        totalFiles++;
        updateProgress();
    });

    connection.on("fileComplete", function (hubId) {
        totalProcessed++;
        updateProgress();
    });

    connection.on("hubComplete", function (hubId) {
        done = true;
        updateProgress();
    });
}

function updateProgress(suffix) {
    $('.progress-bar').attr('aria-valuemax', totalFiles);
    $('.progress-bar').css('width', Math.round(totalProcessed / totalFiles * 100) + '%').attr('aria-valuenow', totalProcessed);
    $('#indexProgressBar span').text(totalProcessed + ' of ' + totalFiles + ' processed. ' + (done ? '' : ' Searching...'))

    if (done && totalFiles===totalProcessed){
        $('.progress-bar').removeClass('progress-bar-info').removeClass('active').addClass('progress-bar-success');
        $('#indexProgressBar span').text('Index complete. Ready to search on this account.');
    }
}