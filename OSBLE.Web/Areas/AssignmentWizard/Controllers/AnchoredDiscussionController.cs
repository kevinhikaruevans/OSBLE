﻿using System.Collections.Generic;
using System.Data.Entity.Validation;
using System.Diagnostics;
using System.Linq;
using System.Web.Mvc;
using OSBLE.Controllers;
using OSBLE.Models.Assignments;
using OSBLE.Models.Courses;
using OSBLE.Utility;
using System;
using OSBLE.Models.HomePage;
using OSBLE.Areas.AssignmentWizard.Models;
using OSBLE.Attributes;
using OSBLE.Models.FileSystem;

namespace OSBLE.Areas.AssignmentWizard.Controllers
{
    public class AnchoredDiscussionController : WizardBaseController
    {
        public override string PrettyName
        {
            get { return "Anchored Discussion Settings"; }
        }

        public override string ControllerName
        {
            get { return "AnchoredDiscussion"; }
        }

        public override string ControllerDescription
        {
            get
            {
                return "This assignment includes one or more documents students can collaboratively annoate";
            }
        }

        public override IWizardBaseController Prerequisite
        {
            get
            {                
                return new AnchoredDiscussionDocumentsController();
                
            }
        }

        public override ICollection<AssignmentTypes> ValidAssignmentTypes
        {
            get
            {
                return (new AssignmentTypes[] { AssignmentTypes.AnchoredDiscussion }).ToList();
            }
        }

        public override bool IsRequired
        {
            get
            {
                return true;
            }
        }

        public override ActionResult Index()
        {
            base.Index();
            ModelState.Clear();
            return View(Assignment);
        }

        [HttpPost]
        public ActionResult Index(Assignment model)
        {
            return base.PostBack(model);
        }

    }
}