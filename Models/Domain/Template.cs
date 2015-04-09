using System;
using System.Collections.Generic;
using System.Data.Objects.DataClasses;
using System.Linq;
using System.Web;
using eSign.Models.Domain;
using System.ComponentModel.DataAnnotations;

namespace eSign.WebAPI.Models.Domain
{
    public class Template
    {
        public int TemplateCode { get; set; }
        public string TemplateName { get; set; }
        public string TemplateDescription { get; set; }
        public DateTime CreatedDateTime { get; set; }
        public bool IsTemplateEditable { get; set; }
        public bool IsTemplateDeleted { get; set; }       
    }

  
    public class TemplateSend
    {
        public int TemplateCode { get; set; }
        public List<EmailDetails> Recipients { get; set; }
        public string MailSubject { get; set; }
        public string MailBody { get; set; }        
    }

  

    public class EmailDetails
    {
        [Required]
        [RegularExpression("^[a-zA-Z .]+$", ErrorMessage = "Use letters and . only please")]
        [StringLength(25, ErrorMessage = "Name can not be greater that 25 characters.")]
        public string Role { get; set; }

        [Required]
        [RegularExpression(@"^[a-zA-Z0-9_\+-]+(\.[a-zA-Z0-9_\+-]+)*@[a-zA-Z0-9-]+(\.[a-zA-Z0-9-]+)*\.([a-zA-Z]{2,4})$", ErrorMessage = "Please enter valid email id.")]
        [StringLength(256, ErrorMessage = "Email Id can not be greater that 256 characters.")]
        public string EmailAddress { get; set; } 
    }
   
    public class TemplateDetails
    {
        public int TemplateCode { get; set; }
        public string TemplateName { get; set; }
        public string TemplateDescription { get; set; }
        public DateTime CreatedDateTime { get; set; }
        public bool IsTemplateEditable { get; set; }
        public bool IsTemplateDeleted { get; set; }
        public Guid DateFormatID { get; set; }        
        public DateTime ExpiryDateTime { get; set; }
        public Guid ExpiryTypeID { get; set; }
        public Guid ID { get; set; }
        public DateTime ModifiedDateTime { get; set; }
        public string PasswordKey { get; set; }
        public int? PasswordKeySize { get; set; }
        public bool PasswordReqdToOpen { get; set; }
        public bool PasswordReqdToSign { get; set; }
        public string PasswordToOpen { get; set; }
        public string PasswordToSign { get; set; }
        public int? RemainderDays { get; set; }
        public int? RemainderRepeatDays { get; set; }
        public Guid StatusID { get; set; }
        public string DateFormat { get; set; }
        public List<DocumentDetails> documentDetails { get; set; }
        public Guid UserID { get; set; }
        public string ExpiryType { get; set; }
        public List<RolesDetails> RoleList { get; set; }
    }

    public class RolesDetails
    {
        public Guid ID { get; set; }
        public Guid TemplateID { get; set; }
        public string RoleName { get; set; }
        public int? Order { get; set; }
        public DateTime CreatedDateTime { get; set; }
        public string RoleType { get; set; }
    }

    public class DocumentDetails
    {        
        public string DocumentName { get; set; }
        public Guid EnveopeID { get; set; }
        public Guid ID { get; set; }
        public short? Order { get; set; }
        public DateTime UploadedDateTime { get; set; }
        public List<DocumentContentDetails> documentContentDetails { get; set; }        
    }


    public class DocumentContentDetails
    {
        public string ControlHtmlData { get; set; }
        public string ControlHtmlID { get; set; }
        public Guid ControlID { get; set; }
        public Guid DocumentID { get; set; }
        public string GroupName { get; set; }
        public int? Height { get; set; }
        public Guid ID { get; set; }
        public string Label { get; set; }
        public int? PageNo { get; set; }
        public int? DocumentPageNo { get; set; }
        public Guid? RecipientID { get; set; }
        public bool Required { get; set; }
        public int? Width { get; set; }
        public int? XCoordinate { get; set; }
        public int? YCoordinate { get; set; }
        public int? ZCoordinate { get; set; }
        public string ControlValue { get; set; }
        public string RecipientName { get; set; }        
        public List<ControlStyleDetails> controlStyleDetails { get; set; }       
        public ControlStyleDetails ControlStyle { get; set; }        
        public List<SelectControlOptionDetails> SelectControlOptions { get; set; }     

    }

    public class ControlStyleDetails
    {
        public string FontName { get; set; }
        public byte FontSize { get; set; }
        public string FontColor { get; set; }
        public bool IsBold { get; set; }
        public bool IsItalic { get; set; }
        public bool IsUnderline { get; set; }
    }


    public class SelectControlOptionDetails
    {
        public Guid DocumentContentID { get; set; }
        public Guid ID { get; set; }
        public string OptionText { get; set; }
        public int Order { get; set; }
    }


}