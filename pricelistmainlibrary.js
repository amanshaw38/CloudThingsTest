// JavaScript source code
"use strict";
var Pricelist = window.Pricelist || {};

/**
 * @function validateYear 
 * @description End Date Should not be greater than Start Date and Year Should be same
 * @return {void} void
 */
Pricelist.validateYear = function (executionContext) {
    var startDateAttributeSchema = "begindate";
    var endDateAttributeSchema = "enddate";


    ///Logic
    var formContext = executionContext.getFormContext();
    var startDate = formContext.getAttribute(startDateAttributeSchema).getValue();
    var endDate = formContext.getAttribute(endDateAttributeSchema).getValue();

    if (endDate <= startDate) {
        formContext.getControl(endDateAttributeSchema).setNotification("End Date must not be before Start Date.", "100");
        formContext.getAttribute(endDateAttributeSchema).setValue(null);
    } else {
        formContext.getControl(endDateAttributeSchema).clearNotification("100");

    }
    var startYear = new Date(startDate);
    var endYear = new Date(endDate);
    if (startYear.getFullYear() === endYear.getFullYear()) {
        formContext.getControl(endDateAttributeSchema).clearNotification("200");

    }
    else {
        formContext.getControl(endDateAttributeSchema).setNotification("Start Date and End Date should be in same year", "200");
    }
};