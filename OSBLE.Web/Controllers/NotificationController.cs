﻿using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using OSBLE.Models.Assignments;
using OSBLE.Models.Courses;
using OSBLE.Models.HomePage;
using OSBLE.Models.Users;
using System.Net.Mail;
using System.Configuration;
using OSBLE.Attributes;
using OSBLE.Utility;
using System;

namespace OSBLE.Controllers
{
    [OsbleAuthorize]
    public class NotificationController : OSBLEController
    {
        //
        // GET: /Notification/
        public ActionResult Index()
        {
            ViewBag.HideMail = OSBLE.Utility.DBHelper.GetAbstractCourseHideMailValue(ActiveCourseUser.AbstractCourseID);

            var notifications = db.Notifications.Where(n => (n.RecipientID == ActiveCourseUser.ID)).OrderByDescending(n => n.Posted).ToList();

            //need to rename sender on anonymous posts
            foreach (var notification in notifications)
            {
                if (notification.Data != null && notification.Data == "IsAnonymous")
                {
                    CourseUser anonUser = new CourseUser();
                    anonUser.UserProfile = new UserProfile();
                    anonUser.UserProfile.FirstName = "Anonymous";
                    anonUser.UserProfile.LastName = "User";
                    notification.Sender = anonUser;
                }
            }

            ViewBag.Notifications = notifications;

            return View();
        }

        public ActionResult Dispatch(int id)
        {
            Notification n = db.Notifications.Find(id);

            if (n != null)
            {
                CourseUser recipient = db.CourseUsers.FirstOrDefault(cu => cu.ID == n.RecipientID);

                // Notification exists and belongs to current user.
                if (recipient.UserProfileID == ActiveCourseUser.UserProfileID)
                {
                    // Mark notification as read.
                    n.Read = true;
                    db.SaveChanges();

                    // Determine which item type and dispatch to the appropriate action/controller
                    switch (n.ItemType)
                    {
                        case Notification.Types.Mail:
                            return RedirectToAction("View", "Mail", new { ID = n.ItemID });
                        case Notification.Types.EventApproval:
                            return RedirectToAction("Approval", "Event", new { ID = n.ItemID });
                        case Notification.Types.Dashboard:
                            var DashBoardRead = (from d in db.Notifications
                                                 where d.RecipientID == ActiveCourseUser.ID && d.ItemType == Notification.Types.Dashboard && d.ItemID == n.ItemID
                                                 select d).ToList();
                            foreach (var foo in DashBoardRead)
                            {
                                foo.Read = true;
                            }
                            db.SaveChanges();
                            return RedirectToAction("ViewThread", "Home", new { ID = n.ItemID });
                        case Notification.Types.FileSubmitted:
                            return RedirectToAction("getCurrentUsersZip", "FileHandler", new { assignmentID = n.ItemID });
                        case Notification.Types.InlineReviewCompleted:
                            return RedirectToAction("Details", "PeerReview", new { ID = n.ItemID });
                        case Notification.Types.RubricEvaluationCompleted:
                            return RedirectToAction("View", "Rubric", new { assignmentId = n.ItemID, cuID = n.RecipientID });
                        case "CriticalReview":
                            string[] splitArray = n.Data.Split(';');
                            return RedirectToAction("ViewForCriticalReview", "Rubric", new { assignmentId = n.ItemID, authorTeamId = splitArray[3] });
                        case Notification.Types.TeamEvaluationDiscrepancy:
                            //n.Data = PrecedingTeamId + ";" + TeamEvaluationAssignment.ID;
                            int precteamId = 0;
                            int teamEvalAssignmnetID = 0;
                            int.TryParse(n.Data.Split(';')[0], out precteamId);
                            int.TryParse(n.Data.Split(';')[1], out teamEvalAssignmnetID);
                            return RedirectToAction("TeacherTeamEvaluation", "Assignment", new { precedingTeamId = precteamId, TeamEvaluationAssignmentId = teamEvalAssignmnetID });
                        case Notification.Types.JoinCourseApproval:
                            //this is done to mark all notifications that were consolidated as read
                            var CourseMarkRead = (from d in db.Notifications
                                                  where d.RecipientID == ActiveCourseUser.ID && d.ItemType == Notification.Types.JoinCourseApproval && d.ItemID == ActiveCourseUser.AbstractCourseID
                                                  select d).ToList();
                            foreach (var foo in CourseMarkRead)
                            {
                                foo.Read = true;
                            }
                            db.SaveChanges();
                            return RedirectToAction("Index", "Roster", new { ID = n.ItemID });
                        case Notification.Types.JoinCommunityApproval:
                            //this is done to mark all notifications that were consolidated as read
                            var CommunityMarkRead = (from d in db.Notifications
                                                     where d.RecipientID == ActiveCourseUser.ID && d.ItemType == Notification.Types.JoinCourseApproval && d.ItemID == ActiveCourseUser.AbstractCourseID
                                                     select d).ToList();
                            foreach (var foo in CommunityMarkRead)
                            {
                                foo.Read = true;
                            }
                            db.SaveChanges();
                            return RedirectToAction("Index", "Roster", new { ID = n.ItemID });
                        case Notification.Types.UserTag:
                            var UserTagMarkRead = (from d in db.Notifications
                                                   where d.ID == n.ID && d.RecipientID == ActiveCourseUser.ID
                                                   select d).ToList();
                            foreach (var foo in UserTagMarkRead)
                            {
                                foo.Read = true;
                            }
                            db.SaveChanges();
                            return RedirectToAction("Details", "Feed", new { id = n.ItemID });
                        default:
                            return RedirectToAction("Index", "Home");
                    }
                }
            }

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        public ActionResult MarkAsRead(int id)
        {
            Notification n = db.Notifications.Find(id);

            // Notification exists and belongs to current user.
            if ((n != null) && (n.RecipientID == ActiveCourseUser.ID))
            {
                // Mark notification as read.
                n.Read = true;
                db.SaveChanges();
            }
            else
            {
                // Return forbidden.
                Response.StatusCode = 403;
            }

            return View("_AjaxEmpty");
        }

        [HttpPost]
        public ActionResult MarkAllAsRead()
        {
            List<Notification> allUnreadNotifications = (from n in db.Notifications
                                                         where n.RecipientID == ActiveCourseUser.ID &&
                                                         !n.Read
                                                         select n).ToList();

            foreach (Notification n in allUnreadNotifications)
            {
                // Mark notification as read.
                n.Read = true;
                db.SaveChanges();
            }

            return View("_AjaxEmpty");
        }

        /// <summary>
        /// Creates and posts a new mail message notification to its recipient
        /// </summary>
        /// <param name="mail">The message that has been sent</param>
        /// <param name="db">The current database context</param>
        [NonAction]
        public void SendMailNotification(Mail mail)
        {
            // Send notification to recipient about new message.
            Notification n = new Notification();
            n.ItemType = Notification.Types.Mail;
            n.ItemID = mail.ID;
            CourseUser recipient = db.CourseUsers.FirstOrDefault(cu => cu.UserProfileID == mail.ToUserProfileID && cu.AbstractCourseID == mail.ContextID);
            //CourseUser recipient = db.CourseUsers.FirstOrDefault(cu => cu.UserProfileID == mail.ToUserProfileID); //FLAG THIS LINE
            if (recipient != null)
                n.RecipientID = recipient.ID;
            CourseUser sender = db.CourseUsers.FirstOrDefault(cu => cu.UserProfileID == mail.FromUserProfileID && cu.AbstractCourseID == mail.ContextID);
            //CourseUser sender = db.CourseUsers.FirstOrDefault(cu => cu.UserProfileID == mail.FromUserProfileID);
            if (sender != null)
                n.SenderID = sender.ID;

            //using the data variable to store context id for email context later.
            n.Data = mail.ContextID.ToString();

            addNotification(n);
        }

        /// <summary>
        /// Posts a notification when others have participated in a dashboard thread which you have participated in
        /// </summary>
        /// <param name="dp">The parent dashboard post</param>
        /// <param name="poster">The user profile of the user who posted the reply</param>
        [NonAction]
        public void SendDashboardNotification(DashboardPost dp, CourseUser poster)
        {
            List<CourseUser> sendToUsers = new List<CourseUser>();

            // Send notification to original thread poster if poster is not anonymized,
            // are still in the course,
            // and are not the poster of the new reply.
            if (poster != null && !poster.AbstractRole.Anonymized && dp.CourseUserID != poster.ID)
            {
                sendToUsers.Add(dp.CourseUser);
            }

            foreach (DashboardReply reply in dp.Replies)
            {
                // Send notifications to each participant as long as they are not anonymized,
                // are still in the course,
                // and are not the poster of the new reply.
                // Also checks to make sure a duplicate notification is not sent.
                if (poster != null && (reply.CourseUser != null && !reply.CourseUser.AbstractRole.Anonymized && reply.CourseUserID != poster.ID && !sendToUsers.Contains(reply.CourseUser)))
                {
                    sendToUsers.Add(reply.CourseUser);
                }
            }

            // Send notification to each valid user.
            foreach (CourseUser courseUser in sendToUsers)
            {
                //ignore null course users
                if (courseUser == null)
                {
                    continue;
                }
                Notification n = new Notification();
                n.ItemType = Notification.Types.Dashboard;
                n.ItemID = dp.ID;
                n.RecipientID = courseUser.ID;
                if (poster != null) n.SenderID = poster.ID;
                addNotification(n);
            }
        }

        public void SendUserTagNotifications(CourseUser sender, int postId, List<CourseUser> recipients, bool isAnonymous = false)
        {
            try
            {
                foreach (CourseUser recipient in recipients)
                {
                    Notification n = new Notification();
                    n.ItemType = Notification.Types.UserTag;
                    n.ItemID = postId;
                    n.RecipientID = recipient.ID;
                    if (sender != null) n.SenderID = sender.ID;
                    if (isAnonymous) n.Data = "IsAnonymous";
                    addNotification(n);
                }
            }
            catch
            { // against Dan's better judgement, do nothing!!!!
            }
        }

        /// <summary>
        /// Creates and posts a new request for approval on an event posting and sends to all instructors in a course
        /// </summary>
        /// <param name="e">The event that requires approval</param>
        /// <param name="db">The current database context</param>
        [NonAction]
        public void SendEventApprovalNotification(Event e)
        {
            // Get all instructors in the course.
            List<CourseUser> instructors = (from i in db.CourseUsers
                                            where
                                                i.AbstractCourseID == e.Poster.AbstractCourseID &&
                                                i.AbstractRoleID == (int)CourseRole.CourseRoles.Instructor
                                            select i).ToList();

            foreach (CourseUser instructor in instructors)
            {
                Notification n = new Notification();
                n.ItemType = Notification.Types.EventApproval;
                n.ItemID = e.ID;
                n.RecipientID = instructor.ID;
                n.SenderID = e.Poster.ID;

                addNotification(n);
            }
        }

        [NonAction]
        public void SendCourseApprovalNotification(Course c, CourseUser sender)
        {
            // Get all instructors in the course.
            List<CourseUser> instructors = (from i in db.CourseUsers
                                            where
                                              i.AbstractCourseID == c.ID &&
                                              i.AbstractRoleID == (int)CourseRole.CourseRoles.Instructor
                                            select i).ToList();


            foreach (CourseUser instructor in instructors)
            {
                Notification n = new Notification();
                n.ItemType = Notification.Types.JoinCourseApproval;
                n.ItemID = c.ID;
                n.RecipientID = instructor.ID;
                n.SenderID = sender.ID;

                addNotification(n);
            }
            db.SaveChanges();
        }

        [NonAction]
        public void SendCommunityApprovalNotification(Community c, CourseUser sender)
        {
            List<CourseUser> leaders = (from i in db.CourseUsers
                                        where
                                          i.AbstractCourseID == c.ID &&
                                          i.AbstractRoleID == (int)CommunityRole.OSBLERoles.Leader
                                        select i).ToList();

            foreach (CourseUser leader in leaders)
            {
                Notification n = new Notification();
                n.ItemType = Notification.Types.JoinCommunityApproval;
                n.ItemID = c.ID;
                n.RecipientID = leader.ID;
                n.SenderID = sender.ID;


                addNotification(n);

            }
            db.SaveChanges();
        }

        [NonAction]
        public void SendInlineReviewCompletedNotification(Assignment assignment, Team team)
        {
            foreach (TeamMember member in team.TeamMembers)
            {
                Notification n = new Notification();
                n.ItemType = Notification.Types.InlineReviewCompleted;
                n.Data = assignment.ID.ToString() + ";" + team.ID.ToString() + ";" + assignment.AssignmentName;
                n.RecipientID = member.CourseUserID;
                n.SenderID = ActiveCourseUser.ID;
                addNotification(n);
            }
        }

        [NonAction]
        public void SendRubricEvaluationCompletedNotification(Assignment assignment, Team team)
        {

            if (assignment.Type == AssignmentTypes.CriticalReview)
            {
                foreach (TeamMember member in team.TeamMembers)
                {
                    Notification n = new Notification();
                    n.ItemType = "CriticalReview";
                    n.Data = assignment.ID.ToString() + ";" + member.CourseUserID + ";" + assignment.AssignmentName + ";" + team.ID;
                    n.ItemID = assignment.ID;
                    n.RecipientID = member.CourseUser.ID;
                    n.SenderID = ActiveCourseUser.ID;
                    addNotification(n);
                }
            }

            else
            {
                foreach (TeamMember member in team.TeamMembers)
                {
                    Notification n = new Notification();
                    n.ItemType = Notification.Types.RubricEvaluationCompleted;
                    n.Data = assignment.ID.ToString() + ";" + member.CourseUserID + ";" + assignment.AssignmentName;
                    n.ItemID = assignment.ID;
                    n.RecipientID = member.CourseUser.ID;
                    n.SenderID = ActiveCourseUser.ID;
                    addNotification(n);
                }
            }
            
        }

        /// <summary>
        /// Sends a notification saying that the ActiveCourseUser submitted a Team Evaluation with a large percent spread.
        /// </summary>
        /// <param name="TeamEvaluationAssignmentId"></param>
        /// <param name="PrecedingTeamId"></param>
        [NonAction]
        public void SendTeamEvaluationDiscrepancyNotification(int PrecedingTeamId, Assignment TeamEvaluationAssignment)
        {
            //Sender has completed a [url]Team Evaluation with a large percent spread.

            List<int> InstructorIDs = (from cu in db.CourseUsers
                                       where cu.AbstractCourseID == TeamEvaluationAssignment.Course.ID &&
                                       cu.AbstractRoleID == (int)CourseRole.CourseRoles.Instructor
                                       select cu.ID).ToList();

            foreach (int cuID in InstructorIDs)
            {
                Notification n = new Notification();
                n.ItemType = Notification.Types.TeamEvaluationDiscrepancy;
                n.Data = PrecedingTeamId + ";" + TeamEvaluationAssignment.ID;
                n.RecipientID = cuID;
                n.SenderID = ActiveCourseUser.ID;
                addNotification(n);
            }
        }

        [NonAction]
        public void SendFilesSubmittedNotification(Assignment assignment, AssignmentTeam team, string fileName)
        {
            //if (assignment.Category.CourseID == activeCourse.AbstractCourseID)
            //{
            //    var canGrade = (from c in db.CourseUsers
            //                    where c.AbstractCourseID == activeCourse.AbstractCourseID
            //                    && c.AbstractRole.CanGrade
            //                    select c).ToList();

            //    foreach (CourseUser user in canGrade)
            //    {
            //        Notification n = new Notification();
            //        n.ItemType = Notification.Types.FileSubmitted;
            //        n.Data = assignment.ID.ToString() + ";" + team.TeamID.ToString() + ";" + assignment.AssignmentName + ";" + team.Team.Name + ";" + fileName + ";" + DateTime.UtcNow;
            //        n.RecipientID = user.ID;
            //        n.SenderID = activeCourse.ID;
            //        addNotification(n);
            //    }
            //}
        }

        /// <summary>
        /// Adds notification to the db,
        /// and calls emailNotification if recipient's
        /// settings request notification emails.
        /// </summary>
        /// <param name="n">Notification to be added</param>
        private void addNotification(Notification n)
        {
            try
            {
                db.Notifications.Add(n);
                db.SaveChanges();

                // Find recipient profile and check notification settings
                CourseUser recipient = (from a in db.CourseUsers
                                        where a.ID == n.RecipientID
                                        select a).FirstOrDefault();
                bool isObserver = recipient.AbstractRoleID == (int)CourseRole.CourseRoles.Observer ? true : false;
#if !DEBUG
                //If the recipient wants to receive e-mail notifications and they are not an Observer, send them an e-mail notification
                if (recipient.UserProfile.EmailAllNotifications && !isObserver)
                {
                    emailNotification(n);
                }
#endif
            }
            catch (Exception e)
            {
                //possibly null sender or recipient
                //TODO: address why we might get null
            }
        }

        /// <summary>
        /// Sends an email notification to a user.
        /// Does not run in debug mode.
        /// </summary>
        /// <param name="n">Notification to be emailed</param>
        private void emailNotification(Notification n)
        {
            try
            {
                SmtpClient mailClient = new SmtpClient();
                mailClient.UseDefaultCredentials = true;
                //this line causes a break if the course user is not part of any courses
                if (n.Sender == null)
                {
                    n.Sender = db.CourseUsers.Find(ActiveCourseUser.ID);
                }

                UserProfile sender = db.UserProfiles.Find(n.Sender.UserProfileID);
                UserProfile recipient = db.UserProfiles.Find(n.Recipient.UserProfileID);

                //Abstract course can represent a course or a community 
                AbstractCourse course = db.AbstractCourses.Where(b => b.ID == n.CourseID).FirstOrDefault();
                string[] temp;
                //checking to see if there is no data besides abstractCourseID
                if (n.Data != null && n.Data != "IsAnonymous")
                {
                    temp = n.Data.Split(';');
                }
                else
                {
                    temp = new string[0];
                }

                int id;

                if (temp.Length == 1) //data not being used by other mail method, send from selected course
                {
                    id = Convert.ToInt16(temp[0]);
                    course = db.AbstractCourses.Where(b => b.ID == id).FirstOrDefault();
                }

                string subject = "";
                if (getCourseNotificationTag(course, n) != "")
                {
                    subject = getCourseNotificationTag(course, n); // Email subject prefix
                }

                string body = "";

                string action = "";

                switch (n.ItemType)
                {
                    case Notification.Types.Mail:
                        Mail m = db.Mails.Find(n.ItemID);
                        subject += " - " + sender.FirstName + " " + sender.LastName;

                        action = "reply to this message";
                        //yc: m.posted needs to have a converetd time

                        body = sender.FirstName + " " + sender.LastName + " sent this message at " + m.Posted.UTCToCourse(ActiveCourseUser.AbstractCourseID).ToString() + ":\n\n";
                        body += "Subject: " + m.Subject + "\n\n";
                        body += m.Message;

                        break;
                    case Notification.Types.EventApproval:
                        subject += " - " + sender.FirstName + " " + sender.LastName;

                        body = sender.FirstName + " " + sender.LastName + " has requested your approval of an event posting.";

                        action = "approve/reject this event.";

                        break;
                    case Notification.Types.Dashboard:
                        subject += " - " + sender.FirstName + " " + sender.LastName;

                        body = sender.FirstName + " " + sender.LastName + " has posted to an activity feed thread in which you have participated.";

                        action = "view this activity feed thread.";

                        break;
                    case Notification.Types.FileSubmitted:
                        subject += " - " + sender.FirstName + " " + sender.LastName;

                        body = n.Data; //sender.FirstName + " " + sender.LastName + " has submitted an assignment."; //Can we get name of assignment?

                        action = "view this assignment submission.";

                        break;
                    case Notification.Types.RubricEvaluationCompleted:
                        subject += " - " + sender.FirstName + " " + sender.LastName;

                        body = sender.FirstName + " " + sender.LastName + " has published a rubric for your assignment."; //Can we get name of assignment?

                        action = "view this assignment submission.";

                        break;

                    case "CriticalReview":
                        subject += " - " + sender.FirstName + " " + sender.LastName;

                        body = sender.FirstName + " " + sender.LastName + " has published a critical review for your assignment."; //Can we get name of assignment?

                        action = "view this assignment submission.";

                        break;
                    case Notification.Types.InlineReviewCompleted:
                        subject += " - " + sender.FirstName + " " + sender.LastName;

                        body = n.Data; //sender.FirstName + " " + sender.LastName + " has submitted an assignment."; //Can we get name of assignment?

                        action = "view this assignment submission.";

                        break;
                    case Notification.Types.TeamEvaluationDiscrepancy:
                        subject += " - " + sender.FirstName + " " + sender.LastName;

                        body = sender.FirstName + " " + sender.LastName + " has submitted a Team Evaluation that has raised a discrepancy flag."; //Can we get name of assignment?

                        action = "view team evaluation discrepancy.";

                        break;
                    case Notification.Types.JoinCourseApproval:
                        subject += " - " + sender.FirstName + " " + sender.LastName;
                        if (course != null)
                            body = sender.FirstName + " " + sender.LastName + " has submitted a request to join " + course.Name;

                        action = "view the request to join.";

                        break;
                    case Notification.Types.JoinCommunityApproval:
                        subject += " - " + sender.FirstName + " " + sender.LastName;
                        if (course != null)
                            body = sender.FirstName + " " + sender.LastName + " has submitted a request to join " + course.Name;

                        action = "view the request to join.";

                        break;
                    case Notification.Types.UserTag:
                        if (n.Data != null && n.Data == "IsAnonymous")
                        {
                            subject += " - An anonymous user tagged you in a post!";
                            if (course != null)
                            {
                                body = "An anonymous user has tagged you in a post or comment in " + course.Name;
                                action = "view the post or comment.";
                            }
                        }
                        else
                        {
                            subject += " - " + sender.FirstName + " " + sender.LastName + " tagged you in a post!";
                            if (course != null)
                            {
                                body = sender.FirstName + " " + sender.LastName + " has tagged you in a post or comment in " + course.Name;
                                action = "view the post or comment.";
                            }
                        }
                        break;
                    default:
                        subject += "No Email set up for this type of notification";

                        body = "No Email set up for this type of notification of type: " + n.ItemType;
                        break;
                }

                body += "\n\n---\nDo not reply to this email.\n";
                string str = getDispatchURL(n.ID);
                body += string.Format("<br /><br /><a href=\"{0}\">Click this link to {1}</a>", str, action);

                MailAddress to = new MailAddress(recipient.UserName, recipient.DisplayName((int)CourseRole.CourseRoles.Instructor));
                List<MailAddress> recipients = new List<MailAddress>();
                recipients.Add(to);
                Email.Send(subject, body, recipients);
            }
            catch (Exception e)
            {
                throw new Exception("emailNotification(Notification n) failed: " + e.Message, e);
            }
        }

        /// <summary>
        /// Returns tags for either a course or a community as well as a tag for a notification, if one exists for the notification. Otherwise, empty string.
        ///
        /// </summary>
        /// <param name="c" , "n">The abstract course Notification param</param>
        /// <returns>Tag with leading space (" CptS 314") if course or community exists, "" if not.</returns>
        private string getCourseNotificationTag(AbstractCourse c, Notification n)
        {
            string tag = "";

            if (c != null || n != null)
            {
                if (c is Course)
                {
                    tag = "[" + (c as Course).Prefix + " " + (c as Course).Number + "]" + "[" + n.ItemType + "]";
                }
                else if (c is Community)
                {
                    tag = "[" + (c as Community).Nickname + "]" + "[" + n.ItemType + "]";
                }
            }

            return tag;
        }

        /// <summary>
        /// Used to get URL to append to email notifications.
        /// Based on current host URL requested by the client, so it should work on
        /// any deployment of OSBLE.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private string getDispatchURL(int id)
        {
            return context.Request.Url.Scheme + System.Uri.SchemeDelimiter + context.Request.Url.Host + ((context.Request.Url.Port != 80 && context.Request.Url.Port != 443) ? ":" + context.Request.Url.Port : "") + "/Notification/Dispatch/" + id;
        }
    }
}