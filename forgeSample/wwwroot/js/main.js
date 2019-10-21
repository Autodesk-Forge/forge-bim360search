function getAccounts(callback) {
    isAuthenticated(function () {
        jQuery.ajax({
            url: '/api/forge/datamanagement/hubs',
            success: function (hubs) {
                //$('.selectpicker').selectpicker({title: 'Select BIM 360 Account'}).selectpicker('render');
                hubs.forEach(function (hub) {
                    var o = new Option(hub.name, hub.id);
                    /// jquerify the DOM object 'o' so we can use the html method
                    $(o).html(hub.name);
                    $("#bim360accounts").append(o);
                })

                if (hubs.length == 1) {
                    $("#bim360accounts").val(hubs[0].id);
                }
                $('.selectpicker').selectpicker('refresh');
                if (callback) callback(hubs);
            }
        });
    });
}

function isAuthenticated(callback) {
    jQuery.ajax({
        url: '/api/forge/oauth/token',
        success: function (res) {
            // yes, it is signed in...
            $('#signOut').show();

            // prepare sign out
            $('#signOut').click(function () {
                $('#hiddenFrame').on('load', function (event) {
                    location.href = '/api/forge/oauth/signout';
                });
                $('#hiddenFrame').attr('src', 'https://accounts.autodesk.com/Authentication/LogOut');
                // learn more about this signout iframe at
                // https://forge.autodesk.com/blog/log-out-forge
            })

            // finally:
            //showUser();

            if (callback) callback();
        },
        error: function () {
            $('#signInModal').modal('toggle');
        }
    });
}

$(document).ready(function () {
    $('#doSearch').click(function () {
        location.href = '/search?q=' + $('#searchString').val() + '&accountId=' + $('#bim360accounts').val()
    });

    $('#indexAccount').click(function () {
        location.href = '/setup';
    });

    $('#autodeskSigninButton').click(function () {
        jQuery.ajax({
            url: '/api/forge/oauth/url',
            success: function (url) {
                location.href = url;
            }
        });
    });

    $(document).keypress(function (e) {
        if (e.keyCode === 13) {
            if ($("#searchString").is(":focus")) {
                $("#doSearch").click();
            }
            return false;
        }
    });
});