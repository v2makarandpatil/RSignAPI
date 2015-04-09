﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace eSign.WebAPI.Models.Domain
{
    public class Control
    {
        public int XCordinate { get; set; }
        public int YCordinate { get; set; }
        public int ZCordinate { get; set; }
        public string DocumentContentId { get; set; }
        public string DocumentId { get; set; }
        public string PageName { get; set; }
        public string EnvelopeId { get; set; }
    }

    public class Signature : Control
    {
        public bool Required { get; set; }
        public string RecipientId { get; set; }
        public int Height { get; set; }
        public int Width { get; set; }
    }

    public class Name : Control
    {
        public bool Required { get; set; }
        public string RecipientId { get; set; }
        public int Width { get; set; }
        public string Color { get; set; }
        public byte fontSize { get; set; }
        public string fontFamilyID { get; set; }
        public bool Bold { get; set; }
        public bool Underline { get; set; }
        public bool Italic { get; set; }
    }

    public class Title : Control
    {
        public bool Required { get; set; }
        public string RecipientId { get; set; }
        public int Width { get; set; }
        public string Color { get; set; }
        public byte fontSize { get; set; }
        public string fontFamilyID { get; set; }
        public bool Bold { get; set; }
        public bool Underline { get; set; }
        public bool Italic { get; set; }
    }

    public class Company : Control
    {
        public bool Required { get; set; }
        public string RecipientId { get; set; }
        public int Width { get; set; }
        public string Color { get; set; }
        public byte fontSize { get; set; }
        public string fontFamilyID { get; set; }
        public bool Bold { get; set; }
        public bool Underline { get; set; }
        public bool Italic { get; set; }
    }

    public class Initials : Control
    {
        public bool Required { get; set; }
        public string RecipientId { get; set; }
        public string Color { get; set; }
        public byte fontSize { get; set; }
        public string fontFamilyID { get; set; }
        public bool Bold { get; set; }
        public bool Underline { get; set; }
        public bool Italic { get; set; }
    }

    public class Label : Control
    {
        public int Width { get; set; }
        public string Color { get; set; }
        public byte fontSize { get; set; }
        public string fontFamilyID { get; set; }
        public bool Bold { get; set; }
        public bool Underline { get; set; }
        public bool Italic { get; set; }
        public string Text { get; set; }
    }

    public class Date : Control
    {
        public bool Required { get; set; }
        public string RecipientId { get; set; }
    }

    public class Radio : Control
    {
        public string RecipientId { get; set; }
        public string GroupName { get; set; }

    }

    public class Checkbox : Control
    {
        public bool Required { get; set; }
        public string RecipientId { get; set; }

    }

    public class Text : Control
    {
        public bool Required { get; set; }
        public string RecipientId { get; set; }
        public int Width { get; set; }
        public string Color { get; set; }
        public byte fontSize { get; set; }
        public string fontFamilyID { get; set; }
        public bool Bold { get; set; }
        public bool Underline { get; set; }
        public bool Italic { get; set; }
        public string maxcharID { get; set; }
        public string textTypeID { get; set; }
        public string LabelText { get; set; }
    }

    public class ControlResponse
    {
        public Guid EnvelopeID { get; set; }
        public Guid DocumentContentId { get; set; }
    }

    public class DropDownBox : Control
    {
        public bool Required { get; set; }
        public string RecipientId { get; set; }
        public int Width { get; set; }
        public List<SelectOption> SelectOption { get; set; }
    }

    public class SelectOption
    {
        public string Option { get; set; }
    }
}