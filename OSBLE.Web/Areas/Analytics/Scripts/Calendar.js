﻿
var monthNames = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];
var yearG = 2015, monthG = 01, dayG = 01;
var today = new Date();
var currentMonth = today.getMonth() + 1, currentYear = today.getFullYear();

function SetCalendarDate(year, month, day)
{
    // if this happens, there was probably an error. Or the user
    // selected "Any". In either case, we don't want to change the
    // default calendar date.
    if (year < 1900)
        return;

    yearG = currentYear = year;
    monthG = currentMonth = month;
    dayG = day;

    updateMeasureBackground();
    if ($("#hourlychart").is(":visible")) {

        onDayClick(yearG, monthG, dayG, false);
    } else {

        updateCalendar(0);
    }
}

$(document).ready(function () {

    $("#back").click(function () { updateCalendar(-1); });
    $("#forward").click(function () { updateCalendar(1); });

    $("input[type='radio']").click(function () {

        updateRadioDependencies();
        updateCalendar(0);
    });

    $("input[name='SelectedMeasureTypes']").click(function () {

        if ($("input[name='SelectedMeasureTypes']:checked").length < 6) {

            updateMeasureBackground();

            if ($("#hourlychart").is(":visible")) {

                onDayClick(yearG, monthG, dayG, false);
            } else {

                updateCalendar(0);
            }
        } else {

            $(this).prop("checked", false);
            alert("The calendar can only show 5 or less measures at a time!");
        }
    });

    $("#hourly a").click(function () {

        updateCalendar(0);
    });

    $("#user-show-all").click(function () {

        updateMeasureBackground();

        if ($("#hourlychart").is(":visible")) {
            onDayClick(yearG, monthG, dayG, false);
        } else {
            // update checked values before updating calendar
            $(".cb-user").prop("checked", this.checked);
            updateCalendar(0);
        }
    });

    $(".cb-user").click(cbUserClicked);

    updateMeasureBackground();
    updateRadioDependencies();
    updateCalendar(0);
});

function cbUserClicked() {
    updateMeasureBackground();
    if ($("#hourlychart").is(":visible")) {
        onDayClick(yearG, monthG, dayG, false);
    } else {
        updateCalendar(0);
    }
}

function updateCalendar(monthOffset, selectedMonth, selectedYear) {

    d3.select("svg").remove();
    updateDisplayArea(false);

    // collect selected measures
    var measures = getSelectedMeasures();
    var dataservice = $("#main").attr("data-service-path");

    //user used the datepicker to choose a new month/year
    if (selectedMonth != undefined && selectedYear != undefined)
    {        
        currentMonth = parseInt(selectedMonth) + 1; //+1 to account for javascript 0 based month index
        currentYear = selectedYear;
    }
    else //user used the forward/backward button to change months
    {
        currentMonth += monthOffset;
        if (currentMonth > 12) {
            currentMonth = 1;
            currentYear++;
        }
        if (currentMonth < 1) {
            currentMonth = 12;
            currentYear--;
        }
    }    

    var selectedUsers = "";
    $("input[name='userId']").each(function (index, item) {
        if ($(item).prop("checked") === true)
            selectedUsers += $(item).val() + ',';
    });
    // get rid of the last comma.
    if (selectedUsers.length > 0)
        selectedUsers = selectedUsers.substring(0, selectedUsers.length - 1);

    if (measures.length > 0 && selectedUsers.length > 0) {
        $.ajax({
            url: dataservice + "/api/Calendar/",
            //xhrFields: { withCredentials: true },
            type: "GET",
            headers: { "Access-Control-Allow-Origin": dataservice + ".*" },
            data: {
                ReferenceDate: currentYear + "/" + currentMonth + "/01",
                AggregateFunctionId: $("input[name = 'AggregationFunction']").val(),
                CourseId: $("select[name = 'CourseId']").val(),
                SelectedMeasures: measures,
                SubjectUsers: selectedUsers //.get().join(',')
            }
        }).done(function (data) {

            if (data != null) {
                updateDisplayArea(false);

                currentMonth = data.month;
                currentYear = data.year;
                data.month = data.month - 1;
                buildCalendar(data);
            }
            else {
                updateDisplayArea(false);
                showEmptyCalendar();
            }
        });
    }
    else {
        updateDisplayArea(false);
        showEmptyCalendar();
    }
}

function showEmptyCalendar()
{
    var data = { year: currentYear, month: currentMonth - 1, day: 1, measures: [], activities: [] };
    buildCalendar(data);
}

function buildCalendar(data)
{
    $("#currentMonth").text(calendarLabel(data.year, data.month));

    var scalar = $("#chart").width() / 600;

    // calendar
    var chart = d3.trendingCalendar().height(700).onDayClick(onDayClick);
    d3.select("#chart").selectAll("svg").data([data]).enter().append("svg")
        .attr("width", "100%")
        .attr("height", 700*scalar)
        .append("g").attr("transform", "scale(" + scalar + ")")
        .call(chart);

}

function updateMeasureBackground() {

    $("input[name='SelectedMeasureTypes']").each(function (i, e) {
        if (!$(e).is(":checked")) {

            $(e).next().css({ "background-color": "transparent", "color": "#333" });
        } else {

            $(e).next().css({ "background-color": $(e).attr("data-color"), "color": "#fff" });
        }
    });
}

function updateRadioDependencies() {

    var aggVal = $("input[type='radio']:checked").val();

    $("input[name='SelectedMeasureTypes'][agg-func]").each(function () {
        if ($(this).attr("agg-func") === aggVal) {
            var id = $(this).attr("id");
            $(this).attr("disabled", false).next().attr("for", id);
        }
        else
            $(this).prop("checked", false).attr("disabled", true).next().css({ "background-color": "transparent", "color": "#333" }).attr("for", "");
    });
}

function getSelectedMeasures() {
    var measures = [];
    $("input:checkbox[name='SelectedMeasureTypes']:checked").each(function () {
        measures.push(this.value);
    });
    return measures.toString();
}

function onDayClick(year, month, day) {

    updateDisplayArea(true);

    // month is given as a zero based index. We need it to corespond 
    // to the month number (i.e. Jan = 1, Dec =12)
    month++;

    //the year, month, day are originated from Monthly calendar's day click
    //since user could check or uncheck measures
    //these values need to be preserved globally
    yearG = year, monthG = month, dayG = day;

    d3.select("svg").remove();

    var dataservice = $("#main").attr("data-service-path");
    var measures = getSelectedMeasures();

    if (measures.length > 0)
        $.ajax({
            url: dataservice + "/api/CalendarDay/",
            //xhrFields: { withCredentials: true },
            type: "GET",
            headers: { "Access-Control-Allow-Origin": dataservice + ".*" },
            data: {
                ReferenceDate: year + "/" + month + "/" + day,
                AggregateFunctionId: $("input[name = 'AggregationFunction']").val(),
                CourseId: $("select[name = 'CourseId']").val(),
                SelectedMeasures: measures
            }
        }).done(function (data) {

            //var data = JSON.parse(result);

            if (data != null && data.measures.length > 0) {
                // monthNames is a zero-based array
                $("#currentDay").text(monthNames[month-1] + " " + day + ", " + year);
                drawHourlyChart(data);
            }
            else
            {
                $("#currentDay").text("No Data");
            }

            updateDisplayArea(true);
        });
}

function drawHourlyChart(data) {

    var margin = { top: 20, right: 20, bottom: 30, left: 50 },
        width = 600 - margin.left - margin.right,
        height = 300 - margin.top - margin.bottom,
		padding = -100;

    var x = d3.scale.linear().domain([0, 24]).range([0, width]);
    var y = d3.scale.linear().domain([0, data.max]).range([height, 0]);

    var xAxis = d3.svg.axis().scale(x).orient("bottom");
    var yAxis = d3.svg.axis().scale(y).orient("left");

    var svg = d3.select("#hourlychart").append("svg")
                .attr("width", width + margin.left + margin.right)
                .attr("height", height + margin.top + margin.bottom)
              .append("g")
                .attr("transform", "translate(" + margin.left + "," + margin.top + ")");

    var line = d3.svg.line()
        .x(function (d) { return x(d.hour); })
        .y(function (d) { return y(d.value); })
        .interpolate("linear");

    svg.append("g")
        .attr("class", "x axis")
        .attr("transform", "translate(0," + height + ")")
        .call(xAxis);

    svg.append("g")
        .attr("class", "y axis")
        .call(yAxis)
        .append("text")
        .attr("transform", "rotate(-90)")
        .attr("y", 6)
        .attr("dy", ".71em")
        .style("text-anchor", "end");

    for (var m = 0; m < data.measures.length; m++) {

        svg.append("path")
            .attr("class", "line")
            .attr("d", line(data.measures[m].values))
            .style("stroke", data.measures[m].color);
    }

    svg.append("text")
            .attr("text-anchor", "middle")  // this makes it easy to centre the text as the transform is applied to the anchor
            .attr("transform", "translate(" + (padding / 2) + "," + (height / 2) + ")rotate(-90)")  // text is drawn off the screen top left, move down and out and rotate
            .text("Count");

    svg.append("text")
            .attr("text-anchor", "middle")  // this makes it easy to centre the text as the transform is applied to the anchor
            .attr("transform", "translate(" + (width / 2) + "," + (height - (padding / 2)) + ")")  // centre below axis
            .text("24hr Period");
}

function updateDisplayArea(hourly) {

    if (hourly) {
        $(".d3-tip:visible").hide();
        $("#hourly").show();
        $("#calendar").hide();
    }
    else {
        $("#hourly").hide();
        $("#calendar").show();
    }
}

function calendarLabel(year, month) {

    var monthToDisplay = new Date(year, month, 1, 0, 0, 0, 0);
    monthToDisplay.setMonth(month + 1);
    var monthTo = monthToDisplay.getMonth();

    if (month < monthTo) {

        return monthNames[month] + " - " + monthNames[monthTo] + " " + year;
    }
    else {

        return monthNames[month] + " " + year + " - " + monthNames[monthTo] + " " + (year + 1);
    }
}

