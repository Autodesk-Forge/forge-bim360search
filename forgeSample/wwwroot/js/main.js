function getAccounts(callback) {
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
            $('.selectpicker').selectpicker('refresh');
            if (callback) callback(hubs);
        }
    });
}

$(document).ready(function () {
    $('#doSearch').click(function () {
        location.href = '/search?q=' + $('#searchString').val() + '&accountId=' + $('#bim360accounts').val()
    });
});