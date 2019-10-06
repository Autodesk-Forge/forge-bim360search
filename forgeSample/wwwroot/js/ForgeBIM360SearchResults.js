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
    var q = getParameterByName('q');
    var accountId = getParameterByName('accountId');

    $('#searchString').val(q);
    getAccounts(function () {
        $('#bim360accounts').val(accountId);
        $('.selectpicker').selectpicker('refresh');
    });

    jQuery.ajax({
        url: '/api/forge/datamanagement/hubs/' + accountId + '/search?q=' + q,
        success: function (results) {
            if (results.length === 0) {
                $('#resultList').append('<h3>Could not find any results...</h3>');
            }
            results.forEach(function (item) {
                var bim360Url = 'https://docs.b360' + (item._source.folderUrn.indexOf('emea') > 0 ? '.eu' : '') + '.autodesk.com/projects/' + item._source.projectId.replace("b.", '') + '/folders/' + item._source.folderUrn + '/detail/viewer/items/' + item._source.itemUrn;
                $('#resultList').append('<li class="result-item"><img src="/api/forge/modelderivative/' + btoa(item._source.versionUrn).replace("/", "_") + '/thumbnail" class="result-thumbnail" /><h4><a href="' + bim360Url + '">' + item._source.fileName + '</a></h4></li>')
            })
        }
    });
});

function getParameterByName(name, url) {
    if (!url) url = window.location.href;
    name = name.replace(/[\[\]]/g, '\\$&');
    var regex = new RegExp('[?&]' + name + '(=([^&#]*)|&|#|$)'),
        results = regex.exec(url);
    if (!results) return null;
    if (!results[2]) return '';
    return decodeURIComponent(results[2].replace(/\+/g, ' '));
}