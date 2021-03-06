﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Dapper;
using OSBLEPlus.Logic.Utility;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Web.UI.WebControls;
using System.Net.Mail;
using OSBLE.Models.Courses;
using OSBLE.Models.DiscussionAssignment;
using OSBLE.Models.HomePage;
using OSBLE.Models.Users;
using OSBLEPlus.Logic.DomainObjects.ActivityFeeds;
using OSBLEPlus.Logic.DomainObjects.Interface;
using OSBLE.Attributes;
using OSBLE.Models.Assignments;
using OSBLE.Models.Queries;
using OSBLEPlus.Logic.Utility.Lookups;
using OSBLE.Models;
namespace OSBLE.Utility
{
    /// <summary>
    /// This is a class designed to make dapper database calls unified from a central location.
    /// Add any static getter and/or setter for any chunk of data needed from the db here.
    /// Try to follow the standard naming convention used here, as it makes it easier to find
    /// these methods in the future, and prevents duplicate methods.
    /// </summary>
    public static class DBHelper
    {
        public static SqlConnection GetNewConnection()
        {
            return new SqlConnection(StringConstants.ConnectionString);
        }


        /*** Users *********************************************************************************************************/
        #region Users
        public static UserProfile GetUserProfile(int id, SqlConnection connection = null, bool includeProfilePic = false)
        {
            UserProfile profile = null;
            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection()) { profile = GetUserProfile(id, sqlc, includeProfilePic); }
                return profile;
            }

            profile = connection.Query<UserProfile>("SELECT * FROM UserProfiles WHERE ID = @uid",
                new { uid = id }).SingleOrDefault();

            return profile;
        }

        public static ProfileImage GetUserProfileImage(int id, SqlConnection connection = null)
        {
            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection()) { return GetUserProfileImage(id, sqlc); }
            }

            try
            {
                return connection.Query<ProfileImage>("SELECT * FROM ProfileImages WHERE UserID = @uid",
                    new { uid = id }).Single();
            }
            catch
            {
                return null;
            }
        }

        public static void SetUserProfileImage(int id, byte[] pic, SqlConnection connection = null)
        {
            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection()) { SetUserProfileImage(id, pic, sqlc); }
                return;
            }

            // check if we need to insert or update
            if (GetUserProfileImage(id, connection) == null) // insert
            {
                connection.Execute(@"INSERT ProfileImages(UserID, Picture) VALUES (@uid, @picture)",
                    new { uid = id, picture = pic });
            }
            else // update
            {
                connection.Execute(@"UPDATE ProfileImages SET Picture = @picture WHERE UserID =  @uid",
                    new { uid = id, picture = pic });
            }
        }

        public static CourseUser GetCourseUserFromProfileAndCourse(int userProfileID, int courseID, SqlConnection connection = null)
        {
            CourseUser cu = null;
            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection()) { cu = GetCourseUserFromProfileAndCourse(userProfileID, courseID, sqlc); }
                return cu;
            }

            cu = connection.Query<CourseUser>("SELECT * FROM CourseUsers WHERE UserProfileID = @uid AND AbstractCourseID = @cid",
                new { uid = userProfileID, cid = courseID }).FirstOrDefault();

            if (cu != null)
            {
                // Get non-basic data
                AbstractRole role = GetAbstractRole(cu.AbstractRoleID, connection);
                AbstractCourse course = GetAbstractCourse(courseID, connection);
                cu.AbstractRole = role;
                cu.AbstractCourse = course;
            }

            return cu;
        }

        public static List<UserProfile> GetUserProfilesForCourse(int courseId)
        {
            var currentUsers = new List<UserProfile>();
            using (var sqlConnection = new SqlConnection(StringConstants.ConnectionString))
            {
                sqlConnection.Open();

                string query = "SELECT UserProfiles.ID, UserProfiles.FirstName, UserProfiles.LastName " +
                               "FROM UserProfiles " +
                               "INNER JOIN CourseUsers " +
                               "ON UserProfiles.ID = CourseUsers.UserProfileID " +
                               "WHERE CourseUsers.AbstractCourseID = @courseId ";

                currentUsers = sqlConnection.Query<UserProfile>(query, new { courseId = courseId }).ToList();

                sqlConnection.Close();
            }
            return currentUsers;
        }

        /// <param name="identification"> The student's identification number as a string (found in UserProfile.Identification) </param>
        /// <param name="abstractCourseId"> The ID of the course in question. </param>
        /// <returns> True if student is in the course, false otherwise. </returns>
        /// courtney-snyder
        public static bool IsUserInCourse (string identification, int abstractCourseId )
        {
            using (var sqlConnection = new SqlConnection(StringConstants.ConnectionString))
            {
                sqlConnection.Open();

                string query = "SELECT * " +
                               "FROM UserProfiles " +
                               "INNER JOIN CourseUsers " +
                               "ON UserProfiles.ID = CourseUsers.UserProfileID " +
                               "WHERE CourseUsers.AbstractCourseID = @courseId " +
                               "AND UserProfiles.Identification = @id ";

                var result = sqlConnection.Query(query, new { id = identification, courseId = abstractCourseId }).FirstOrDefault();

                sqlConnection.Close();

                return result == null ? false : true;
            }
        }

        public static AbstractRole GetAbstractRole(int roleID, SqlConnection connection = null)
        {
            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection()) { return GetAbstractRole(roleID, sqlc); }
            }

            string discriminator = connection.Query<string>("SELECT Discriminator FROM AbstractRoles WHERE ID = @id", new { id = roleID }).Single();
            AbstractRole role = null;
            switch (discriminator)
            {
                case "CourseRole":
                    role = connection.Query<CourseRole>("SELECT * FROM AbstractRoles WHERE ID = @id", new { id = roleID }).Single();
                    break;
                case "CommunityRole":
                    role = connection.Query<CommunityRole>("SELECT * FROM AbstractRoles WHERE ID = @id", new { id = roleID }).Single();
                    break;
                case "AssessmentCommitteeChairRole":
                    role = connection.Query<AssessmentCommitteeChairRole>("SELECT * FROM AbstractRoles WHERE ID = @id", new { id = roleID }).Single();
                    break;
                case "AssessmentCommitteeMemberRole":
                    role = connection.Query<AssessmentCommitteeMemberRole>("SELECT * FROM AbstractRoles WHERE ID = @id", new { id = roleID }).Single();
                    break;
                case "ABETEvaluatorRole":
                    role = connection.Query<ABETEvaluatorRole>("SELECT * FROM AbstractRoles WHERE ID = @id", new { id = roleID }).Single();
                    break;
            }

            return role;
        }

        public static UserProfile GetUserProfile(string userName, SqlConnection connection = null)
        {
            UserProfile profile = null;
            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection())
                {
                    profile = GetUserProfile(userName, sqlc);
                }
                return profile;
            }

            profile = connection.Query<UserProfile>("SELECT * FROM UserProfiles WHERE UserName = @UserName",
                new { UserName = userName }).SingleOrDefault();

            return profile;
        }

        public static int GetUserProfileIndexForName(string firstName, string lastName, SqlConnection connection = null)
        {
            int index = -1;
            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection())
                {
                    index = GetUserProfileIndexForName(firstName, lastName, sqlc);
                }
                return index;
            }

            index = connection.Query<int>("SELECT Id FROM UserProfiles WHERE FirstName = @FirstName AND LastName = @LastName",
                new { FirstName = firstName, LastName = lastName }).FirstOrDefault();

            return index;
        }

        public static List<int> GetCourseInstructorIds(int courseId, SqlConnection connection = null)
        {
            List<int> courseInstructorIds = new List<int>();
            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection())
                {
                    courseInstructorIds = GetCourseInstructorIds(courseId, sqlc);
                }
                return courseInstructorIds;
            }

            courseInstructorIds = connection.Query<int>("SELECT UserProfileID FROM CourseUsers WHERE AbstractRoleID = @abstractRoleId AND AbstractCourseID = @courseId ",
                new { abstractRoleId = (int)CourseRole.CourseRoles.Instructor, courseId = courseId }).ToList();

            return courseInstructorIds;
        }

        public static List<int> GetCourseTAIds(int courseId, SqlConnection connection = null)
        {
            List<int> courseTAIds = new List<int>();
            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection())
                {
                    courseTAIds = GetCourseTAIds(courseId, sqlc);
                }
                return courseTAIds;
            }

            courseTAIds = connection.Query<int>("SELECT UserProfileID FROM CourseUsers WHERE AbstractRoleID = @abstractRoleId AND AbstractCourseID = @courseId ",
                new { abstractRoleId = (int)CourseRole.CourseRoles.TA, courseId = courseId }).ToList();

            return courseTAIds;
        }

        public static List<int> GetCourseSectionUserProfileIds(int courseId, int section, SqlConnection connection = null)
        {
            List<int> courseSectionUserProfileIds = new List<int>();

            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection())
                {
                    courseSectionUserProfileIds = GetCourseSectionUserProfileIds(courseId, section, sqlc);
                }
                return courseSectionUserProfileIds;
            }

            string query = @"SELECT UserProfileID FROM CourseUsers WHERE AbstractCourseID = @courseId AND Section IN (@section, -2) 
                             SELECT UserProfileID, MultiSection FROM CourseUsers WHERE AbstractCourseID = @courseId AND Section = -1 ";
            List<string> multiSection = new List<string>();

            using (var multi = connection.QueryMultiple(query, new { courseId = courseId, section = section }))
            {
                courseSectionUserProfileIds = multi.Read<int>().ToList();
                var sectionList = multi.Read<dynamic>().ToList();

                foreach (dynamic result in sectionList)
                {
                    if (result.MultiSection.Contains(section.ToString()))
                    {
                        courseSectionUserProfileIds.Add(result.UserProfileID);
                    }
                }
            }
            return courseSectionUserProfileIds;
        }

        public static List<int> GetCourseSections(int courseId, SqlConnection connection = null)
        {
            List<int> courseSections = new List<int>();
            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection())
                {
                    courseSections = GetCourseSections(courseId, sqlc);
                }
                return courseSections;
            }

            var results = connection.Query<dynamic>("SELECT Section, MultiSection FROM CourseUsers WHERE AbstractCourseID = @courseId ",
                new { courseId = courseId });

            foreach (dynamic result in results)
            {
                if (result.Section == -1) //multi-section
                {
                    string multisection = result.MultiSection;
                    List<string> sectionIds = multisection.Split(',').ToList();
                    if (sectionIds.Count() > 1 && !sectionIds.Equals("all")) //just in case...
                    {
                        foreach (string id in sectionIds)
                        {
                            int parsedId = 0;
                            bool parseIdToInt = int.TryParse(id, out parsedId);
                            if (parseIdToInt) //only add ids that were successfully parsed            
                                courseSections.Add(parsedId);
                        }
                    }
                }
                else
                {
                    if (result.Section != -2) //ignore all section users
                    {
                        courseSections.Add(result.Section);
                    }
                }
            }
            return courseSections.Distinct().ToList(); //return distinct list of sections
        }

        public static string GetEventLogVisibilityGroups(int eventLogId, SqlConnection connection = null)
        {
            string eventVisibilityGroups = "";
            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection())
                {
                    eventVisibilityGroups = GetEventLogVisibilityGroups(eventLogId, sqlc);
                }
                return eventVisibilityGroups;
            }

            eventVisibilityGroups = connection.Query<string>("SELECT ISNULL(EventVisibilityGroups, '') FROM EventLogs WHERE Id = @eventLogId ",
                new { eventLogId = eventLogId }).Single();

            return eventVisibilityGroups;
        }

        public static string GetEventLogVisibleToList(int eventLogId, SqlConnection connection = null)
        {
            string eventVisibleList = "";
            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection())
                {
                    eventVisibleList = GetEventLogVisibleToList(eventLogId, sqlc);
                }
                return eventVisibleList;
            }

            eventVisibleList = connection.Query<string>("SELECT ISNULL(EventVisibleTo, '') FROM EventLogs WHERE Id = @eventLogId ",
                new { eventLogId = eventLogId }).Single();

            return eventVisibleList;
        }

        public static Dictionary<int, string> GetMailAddressUserId(List<string> emailAddresses, SqlConnection connection = null)
        {
            Dictionary<int, string> UserIdEmailPair = new Dictionary<int, string>();
            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection())
                {
                    UserIdEmailPair = GetMailAddressUserId(emailAddresses, sqlc);
                }
                return UserIdEmailPair;
            }

            var result = connection.Query("SELECT ID, UserName FROM UserProfiles WHERE UserName IN @emailAddresses ",
                new { emailAddresses = emailAddresses }).ToList();

            foreach (var item in result)
            {
                UserIdEmailPair.Add(item.ID, item.UserName);
            }

            return UserIdEmailPair;
        }

        #endregion


        /*** Courses & Communities *****************************************************************************************/
        #region Courses
        public static AbstractCourse GetAbstractCourse(int courseID, SqlConnection connection = null)
        {
            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection()) { return GetAbstractCourse(courseID, sqlc); }
            }

            string discriminator = connection.Query<string>(@"SELECT Discriminator FROM AbstractCourses WHERE ID = @id", new { id = courseID }).SingleOrDefault();
            AbstractCourse course = null;
            switch (discriminator)
            {
                case "Course":
                    course = connection.Query<Course>(@"SELECT * FROM AbstractCourses WHERE ID = @id", new { id = courseID }).SingleOrDefault();
                    break;
                case "Community":
                    course = connection.Query<Community>(@"SELECT * FROM AbstractCourses WHERE ID = @id", new { id = courseID }).SingleOrDefault();
                    break;
            }

            return course;
        }

        public static bool GetAbstractCourseHideMailValue(int courseID, SqlConnection connection = null)
        {
            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection()) { return GetAbstractCourseHideMailValue(courseID, sqlc); }
            }

            bool hideMail = connection.Query<bool>(@"SELECT ISNULL(HideMail, 0) FROM AbstractCourses WHERE ID =  @id", new { id = courseID }).SingleOrDefault();

            return hideMail;
        }

        public static string GetCourseShortNameFromID(int courseID, SqlConnection connection = null)
        {
            string name = "";

            // Set up our connection:
            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection()) { name = GetCourseShortNameFromID(courseID, sqlc); }
            }
            else
            {
                var result = connection.Query<dynamic>("SELECT Prefix, Nickname, Number FROM AbstractCourses WHERE ID = @id", new { id = courseID }).SingleOrDefault();
                if (!String.IsNullOrEmpty(result.Prefix) && !String.IsNullOrEmpty(result.Number))
                    name = result.Prefix + " " + result.Number;
                else
                    name = result.Nickname;
            }

            return name;
        }

        public static DateTime GetCourseStart(int courseID, SqlConnection connection = null)
        {
            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection()) { return GetCourseStart(courseID, sqlc); }
            }

            return connection.Query<DateTime>("SELECT StartDate FROM AbstractCourses WHERE ID = @id", new { id = courseID }).SingleOrDefault();
        }

        public static IEnumerable<dynamic> GetAllCourseUsersFromCourseId(int courseId, SqlConnection connection = null)
        {
            IEnumerable<dynamic> courseUsers = null;

            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection()) { courseUsers = GetAllCourseUsersFromCourseId(courseId, sqlc); }
            }
            else
            {
                courseUsers = connection.Query<dynamic>("SELECT (up.FirstName + ' ' + up.LastName) as 'FullName', up.ID " +
                                                            "FROM CourseUsers cu " +
                                                            "INNER JOIN UserProfiles up " +
                                                            "ON cu.UserProfileID = up.ID " +
                                                            "WHERE AbstractCourseID = @courseId " +
                                                            "ORDER BY FullName",
                                                             new { courseId = courseId });
            }
            return courseUsers;
        }
        public static IEnumerable<CourseUser> GetAllCurrentCourses(int userProfileID, SqlConnection connection = null)
        {
            IEnumerable<CourseUser> currentCourses = null;

            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection()) { currentCourses = GetAllCurrentCourses(userProfileID, sqlc); }
            }
            else
            {
                currentCourses = connection.Query<CourseUser>("SELECT * FROM CourseUsers WHERE UserProfileID = @uid",
                    new { uid = userProfileID });
            }

            return currentCourses;
        }

        public static IEnumerable<CourseUser> GetCoursesFromUserProfileID(int userProfileID, SqlConnection connection = null)
        {
            IEnumerable<CourseUser> courses = null;

            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection()) { courses = GetCoursesFromUserProfileID(userProfileID, sqlc); }
            }
            else
            {
                IEnumerable<int> roleIDs = GetRoleIDsFromDiscriminator("CourseRole", connection);
                courses = connection.Query<CourseUser>(
                     "SELECT * " +
                    "FROM CourseUsers cusers " +
                    "INNER JOIN AbstractCourses acourses " +
                    "ON cusers.AbstractCourseID = acourses.ID " +
                    "WHERE UserProfileID = @uid " +
                    "AND AbstractRoleID IN @rids " +
                    "AND Hidden = 0 " +
                    "AND IsDeleted = 0 ",
                    new { uid = userProfileID, rids = roleIDs });
            }

            return courses;
        }

        public static IEnumerable<CourseUser> GetCommunitiesFromUserProfileID(int userProfileID, SqlConnection connection = null)
        {
            IEnumerable<CourseUser> communities = null;

            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection()) { communities = GetCommunitiesFromUserProfileID(userProfileID, sqlc); }
            }
            else
            {
                IEnumerable<int> roleIDs = GetRoleIDsFromDiscriminator("CommunityRole", connection);
                communities = connection.Query<CourseUser>(
                    "SELECT * " +
                    "FROM CourseUsers cusers " +
                    "INNER JOIN AbstractCourses acourses " +
                    "ON cusers.AbstractCourseID = acourses.ID " +
                    "WHERE UserProfileID = @uid " +
                    "AND AbstractRoleID IN @rids " +
                    "AND Hidden = 0 " +
                    "AND IsDeleted = 0 ",
                    new { uid = userProfileID, rids = roleIDs });
            }

            return communities;
        }


        public static string GetCourseFullNameFromCourseUser(CourseUser cu, SqlConnection connection = null)
        {
            string name = "";

            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection()) { name = GetCourseFullNameFromCourseUser(cu, sqlc); }
            }
            else
            {
                var result = connection.Query<dynamic>("SELECT Name, Prefix, Number, Semester, Year, Inactive, Nickname, Discriminator FROM AbstractCourses WHERE ID = @id",
                    new { id = cu.AbstractCourseID }).SingleOrDefault();

                if (result.Discriminator == "Course")
                    name = string.Format("{0} {1} - {2}, {3}, {4}", result.Prefix, result.Number, result.Name, result.Semester, result.Year);
                else
                    name = string.Format("{0} - {1}", result.Nickname, result.Name);

                // tack role on to the end
                name += " (" + GetAbstractRoleNameFromID(cu.AbstractRoleID, connection) + ")";

                if (null != result.Inactive && result.Inactive)
                    name += " [INACTIVE]";
            }

            return name;
        }

        public static string GetCourseFullNameFromCourseId(int courseId, SqlConnection connection = null)
        {
            string name = "";

            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection()) { name = GetCourseFullNameFromCourseId(courseId, sqlc); }
            }
            else
            {
                var result = connection.Query<dynamic>("SELECT Name, Prefix, Number, Semester, Year, Inactive, Nickname, Discriminator FROM AbstractCourses WHERE ID = @id",
                    new { id = courseId }).SingleOrDefault();

                if (result.Discriminator == "Course")
                    name = string.Format("{0} {1} - {2}, {3}, {4}", result.Prefix, result.Number, result.Name, result.Semester, result.Year);
                else
                    name = string.Format("{0} - {1}", result.Nickname, result.Name);

                // tack role on to the end
                //name += " (" + GetAbstractRoleNameFromID(cu.AbstractRoleID, connection) + ")";

                if (null != result.Inactive && result.Inactive)
                    name += " [INACTIVE]";
            }

            return name;
        }

        public static string GetUserFirstNameFromEventLogId(int eventLogId, SqlConnection connection = null)
        {
            string name = "";

            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection()) { name = GetUserFirstNameFromEventLogId(eventLogId, sqlc); }
            }
            else
            {
                string result = connection.Query<string>("SELECT ISNULL(FirstName, 'OSBLE USER') FROM UserProfiles WHERE ID = (SELECT SenderId FROM EventLogs WHERE Id = @eventLogId)",
                    new { eventLogId = eventLogId }).SingleOrDefault();
                return result;
            }
            return name;
        }

        public static string GetAbstractRoleNameFromID(int ID, SqlConnection connection = null)
        {
            string name = "";

            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection()) { name = GetAbstractRoleNameFromID(ID, sqlc); }
            }
            else
            {
                name = connection.Query<string>("SELECT [Name] FROM AbstractRoles WHERE ID = @id", new { id = ID }).SingleOrDefault();
            }

            return name;
        }

        public static IEnumerable<int> GetRoleIDsFromDiscriminator(string discriminator, SqlConnection connection = null)
        {
            IEnumerable<int> ids = null;

            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection()) { ids = GetRoleIDsFromDiscriminator(discriminator, sqlc); }
            }
            else
            {
                ids = connection.Query<int>("SELECT [ID] FROM AbstractRoles WHERE Discriminator = @disc", new { disc = discriminator });
            }

            return ids;
        }

        public static bool AssignmentDueDatePast(int assignmentId, int abstractCourseId, SqlConnection connection = null)
        {
            if (assignmentId < 1) return false;

            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection())
                {
                    return AssignmentDueDatePast(assignmentId, abstractCourseId, sqlc);
                }
            }

            Assignment a = connection.Query<Assignment>("SELECT * " +
                                                        "FROM Assignments " +
                                                        "WHERE ID = @assignmentId", new { assignmentId }).FirstOrDefault();

            // if assignment is not found, or the current course user is not in the class, return false
            if (a == null || a.CourseID != abstractCourseId) return false;

            DateTime checkDateWithLateHours = a.DueTime.AddHours(a.HoursLateWindow);

            return (DateTime.UtcNow >= checkDateWithLateHours);
        }

        public static DateTime? AssignmentDueDateWithLateHoursInCourseTime(int assignmentId, int abstractCourseId,
            SqlConnection connection = null)
        {
            if (assignmentId < 1) return null;

            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection())
                {
                    return AssignmentDueDateWithLateHoursInCourseTime(assignmentId, abstractCourseId, sqlc);
                }
            }

            Assignment a = connection.Query<Assignment>("SELECT * " +
                                                        "FROM Assignments " +
                                                        "WHERE ID = @assignmentId", new { assignmentId }).FirstOrDefault();

            // if assignment is not found, or the current course user is not in the class, return false
            if (a == null || a.CourseID != abstractCourseId) return null;

            DateTime utcTime = a.DueTime.AddHours(a.HoursLateWindow);

            return utcTime.UTCToCourse(abstractCourseId);
        }

        public static List<int> GetActiveCourseIds(int userProfileId)
        {
            try
            {
                using (var sqlConnection = new SqlConnection(StringConstants.ConnectionString))
                {
                    sqlConnection.Open();

                    string query = "";

                    //we're getting all active course Ids because ask for help events are visible in all courses
                    query = "SELECT DISTINCT ISNULL(ac.ID, 0) " +
                                "FROM AbstractCourses ac " +
                                "INNER JOIN CourseUsers cu " +
                                "ON ac.ID = cu.AbstractCourseID " +
                                "WHERE GETDATE() < ac.EndDate " +
                                "AND ac.Inactive = 0 " +
                                "AND cu.UserProfileID = @userProfileId";

                    List<int> activeCourseIds = sqlConnection.Query<int>(query, new { userProfileId = userProfileId }).ToList();

                    sqlConnection.Close();

                    return activeCourseIds;
                }
            }
            catch (Exception e)
            {
                //TODO: handle exception logging
                return new List<int>(); //failure, return empty list
            }
        }

        public static List<int> GetAllUserCourseIds(int userProfileId)
        {
            try
            {
                using (var sqlConnection = new SqlConnection(StringConstants.ConnectionString))
                {
                    sqlConnection.Open();

                    string query = "";

                    //we're getting all active course Ids because ask for help events are visible in all courses
                    query = "SELECT DISTINCT ISNULL(ac.ID, 0) " +
                                "FROM AbstractCourses ac " +
                                "INNER JOIN CourseUsers cu " +
                                "ON ac.ID = cu.AbstractCourseID " +
                                "AND ac.Inactive = 0 " +
                                "AND cu.UserProfileID = @userProfileId";

                    List<int> activeCourseIds = sqlConnection.Query<int>(query, new { userProfileId = userProfileId }).ToList();

                    sqlConnection.Close();

                    return activeCourseIds;
                }
            }
            catch (Exception e)
            {
                //TODO: handle exception logging
                return new List<int>(); //failure, return empty list
            }
        }

        #endregion


        /*** Discussion Teams **********************************************************************************************/
        #region DiscussionTeams
        public static int GetDiscussionTeamIDFromTeamID(int teamID, SqlConnection connection = null)
        {
            int dtID;

            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection()) { dtID = GetDiscussionTeamIDFromTeamID(teamID, sqlc); }
            }
            else
            {
                dtID = connection.Query<int>(
                        "SELECT Top 1 [ID] FROM DiscussionTeams WHERE TeamID = @id",
                        new { id = teamID }).SingleOrDefault();
            }

            return dtID;
        }

        public static IEnumerable<DiscussionPost> GetDiscussionPosts(int courseUserID, int discussionTeamID, SqlConnection connection = null)
        {
            IEnumerable<DiscussionPost> dps = null;

            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection()) { dps = GetDiscussionPosts(courseUserID, discussionTeamID, sqlc); }
            }
            else
            {
                dps = connection.Query<DiscussionPost>(
                        "SELECT * FROM DiscussionPosts WHERE DiscussionTeamID = @did AND CourseUserID = @cid",
                        new { did = discussionTeamID, cid = courseUserID });
            }

            return dps;
        }

        public static IEnumerable<DiscussionPost> GetInitialDiscussionPosts(int courseUserID, int assignmentID, SqlConnection connection = null)
        {
            IEnumerable<DiscussionPost> dps = null;

            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection()) { dps = GetInitialDiscussionPosts(courseUserID, assignmentID, sqlc); }
            }
            else
            {
                dps = connection.Query<DiscussionPost>(
                    "SELECT * FROM DiscussionPosts WHERE CourseUserID = @cid AND AssignmentID = @aid AND ParentPostID IS NULL",
                    new { cid = courseUserID, aid = assignmentID });
            }

            return dps;
        }

        public static void InsertDiscussionPosts(IEnumerable<DiscussionPost> posts, SqlConnection connection = null)
        {
            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection()) { InsertDiscussionPosts(posts, sqlc); }
            }
            else
            {
                connection.Execute(
                        "INSERT DiscussionPosts (Posted, CourseUserID, Content, AssignmentID, DiscussionTeamID) VALUES (@Posted, @CourseUserID, @Content, @AssignmentID, @DiscussionTeamID)",
                        posts);
            }
        }

        public static bool IsCourse(int courseId)
        {
            AbstractCourse course = GetAbstractCourse(courseId);

            if (course is Course)
            {
                return true;
            }
            return false;
        }
        #endregion


        /*** Events ********************************************************************************************************/
        #region Events
        public static IEnumerable<Event> GetApprovedCourseEvents(int courseID, DateTime start, DateTime end, SqlConnection connection = null)
        {
            IEnumerable<Event> events = null;

            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection()) { events = GetApprovedCourseEvents(courseID, start, end, sqlc); }
                return events;
            }

            events = connection.Query<Event>("SELECT e.* FROM Events e INNER JOIN CourseUsers cu ON e.PosterID = cu.ID WHERE cu.AbstractCourseID = @cid AND e.StartDate >= @sd AND e.StartDate <= @ed AND e.Approved = '1'",
                new { cid = courseID, sd = start, ed = end });

            return events;
        }

        /// <summary>
        /// Returns event log from DB
        /// </summary>
        /// <param name="userProfileId"></param>
        /// <param name="courseId"></param>
        /// <param name="eventId"></param>
        /// <param name="connection"></param>
        /// <returns></returns>
        public static ActivityEvent GetActivityEvent(int eventId, SqlConnection connection = null)
        {
            ActivityEvent evt = null;

            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection())
                {
                    evt = GetActivityEvent(eventId, sqlc);
                }

                return evt;
            }

            evt = connection.Query<ActivityEvent>("SELECT Id AS EventLogId, EventTypeId, EventDate, DateReceived, SenderId, CourseId, SolutionName, IsDeleted, IsAnonymous " +
                                                  "FROM EventLogs e " +
                                                  "WHERE e.Id = @EventId ",
                new { EventId = eventId }
                ).FirstOrDefault();

            return evt;
        }

        public static AskForHelpEvent GetAskForHelpEvent(int eventId, SqlConnection connection = null)
        {
            AskForHelpEvent evt = null;

            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection())
                {
                    evt = GetAskForHelpEvent(eventId, sqlc);
                }

                return evt;
            }

            evt = connection.Query<AskForHelpEvent>("SELECT * " +
                                      "FROM AskForHelpEvents e " +
                                      "WHERE e.EventLogId = @EventId ",
                    new { EventId = eventId }
                    ).FirstOrDefault();

            return evt;
        }

        public static ExceptionEvent GetExceptionEvent(int eventId, SqlConnection connection = null)
        {
            ExceptionEvent evt = null;

            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection())
                {
                    evt = GetExceptionEvent(eventId, sqlc);
                }

                return evt;
            }

            evt = connection.Query<ExceptionEvent>("SELECT * " +
                                      "FROM ExceptionEvents e " +
                                      "WHERE e.EventLogId = @EventId ",
                    new { EventId = eventId }
                    ).FirstOrDefault();

            return evt;
        }

        public static FeedPostEvent GetFeedPostEvent(int eventId, SqlConnection connection = null)
        {
            FeedPostEvent evt = null;

            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection())
                {
                    evt = GetFeedPostEvent(eventId, sqlc);
                }

                return evt;
            }

            evt = connection.Query<FeedPostEvent>("SELECT * " +
                                      "FROM FeedPostEvents e " +
                                      "WHERE e.EventLogId = @EventId ",
                    new { EventId = eventId }
                    ).FirstOrDefault();

            return evt;
        }

        public static List<LogCommentEvent> GetLogCommentEvents(int eventId, SqlConnection connection = null)
        {
            List<LogCommentEvent> comments = null;

            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection())
                {
                    comments = GetLogCommentEvents(eventId, sqlc);
                }

                return comments;
            }

            comments = connection.Query<LogCommentEvent>("SELECT * " +
                                "FROM LogCommentEvents l " +
                                "WHERE l.SourceEventLogId = @EventId",
                                new { EventId = eventId }).ToList();

            return comments;
        }

        public static LogCommentEvent GetSingularLogComment(int eventId, SqlConnection connection = null)
        {
            LogCommentEvent comment = null;

            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection())
                {
                    comment = GetSingularLogComment(eventId, sqlc);
                }

                return comment;
            }

            comment = connection.Query<LogCommentEvent>("SELECT * " +
                                "FROM LogCommentEvents l " +
                                "WHERE l.EventLogId = @EventId",
                                new { EventId = eventId }).FirstOrDefault();

            return comment;
        }

        /// <summary>
        /// Deletes the feed post event, associated eventlog, and asociated LogComments/EventLogs for LogComments
        /// </summary>
        /// <param name="eventId"></param>
        /// <param name="connection"></param>
        public static void DeleteFeedPostEvent(int eventId, SqlConnection connection = null)
        {
            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection())
                {
                    DeleteFeedPostEvent(eventId, sqlc);
                }

                return;
            }

            List<LogCommentEvent> comments = GetLogCommentEvents(eventId, connection);

            List<int> eventLogIds = comments.Select(i => i.EventLogId).ToList();

            // get logCommentEventIds for HelpfulMarks
            List<int> logIds = new List<int>();

            foreach (int id in eventLogIds)
            {
                logIds.AddRange(connection.Query<int>("SELECT Id " +
                                                      "FROM LogCommentEvents " +
                                                      "WHERE EventLogId = @id", new { id }).ToList());
            }

            List<int> helpfulMarkLogIds = new List<int>();

            // Get HelpfulMarks EventLogIds
            foreach (int id in logIds)
            {
                helpfulMarkLogIds.AddRange(connection.Query<int>("SELECT EventLogId " +
                                                                 "FROM HelpfulMarkGivenEvents " +
                                                                 "WHERE LogCommentEventId = @id", new { id }).ToList());
            }

            // need to get all logIds from comments then markhelpful delete as well

            //connection.Query<int>("SELECT EventLogId " +
            //"FROM HelpfulMarkGivenEvents " +
            //"WHERE LogCommentEventId = @logIds", logIds).ToList();


            if (comments.Count > 0)
            {
                // soft delete eventlogs associated with LogComments
                connection.Execute("UPDATE EventLogs " +
                                   "SET IsDeleted = 1" +
                                   "WHERE Id = @EventLogId", comments);
            }

            // soft delete eventlog for feedpost
            connection.Execute("UPDATE EventLogs " +
                   "SET IsDeleted = 1 " +
                   "WHERE Id = @EventLogId", new { EventLogId = eventId });

            // soft delete MarkHelpfulComment EventLog
            foreach (int id in helpfulMarkLogIds)
            {
                connection.Execute("UPDATE EventLogs " +
                                  "SET IsDeleted = 1 " +
                                  "WHERE Id = @EventLogId", new { EventLogId = id });
            }

        }

        public static void DeleteLogComment(int eventId, SqlConnection connection = null)
        {
            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection())
                {
                    DeleteLogComment(eventId, sqlc);
                }

                return;
            }

            LogCommentEvent l = GetSingularLogComment(eventId, connection);
            int logId = connection.Query<int>("Select Id " +
                                           "FROM LogCommentEvents " +
                                           "WHERE EventLogId = @eventId", new { eventId }).SingleOrDefault();

            List<int> helpfulMarkLogIds =
                connection.Query<int>("SELECT EventLogId " +
                                      "FROM HelpfulMarkGivenEvents " +
                                      "WHERE LogCommentEventId = @logId", new { logId }).ToList();


            // soft delete eventlog
            if (l != null)
            {
                connection.Execute("UPDATE EventLogs " +
                                   "SET IsDeleted = 1 " +
                                   "WHERE Id = @EventLogId", l);

                connection.Execute("UPDATE EventLogs " +
                                   "SET IsDeleted = 1 " +
                                   "WHERE Id = @helpfulMarkLogIds", new { helpfulMarkLogIds });
            }
        }

        public static void EditFeedPost(int eventId, string newText, SqlConnection connection = null)
        {
            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection())
                {
                    EditFeedPost(eventId, newText, sqlc);
                }

                return;
            }

            FeedPostEvent fpe = GetFeedPostEvent(eventId);

            // make sure the event exists and the text was changed
            if (fpe != null && fpe.Comment != newText)
            {
                connection.Execute("UPDATE FeedPostEvents " +
                                   "SET Comment = @Comment " +
                                   "WHERE EventLogId = @EventLogId ", new { Comment = newText, EventLogId = eventId });
            }


        }

        public static void EditLogComment(int eventId, string newText, SqlConnection connection = null)
        {
            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection())
                {
                    EditLogComment(eventId, newText, sqlc);
                }

                return;
            }

            LogCommentEvent lce = GetSingularLogComment(eventId);

            // make sure the event exists and the text was changed
            if (lce != null && lce.Content != newText)
            {
                connection.Execute("UPDATE LogCommentEvents " +
                                   "SET Content = @Content " +
                                   "WHERE EventLogId = @EventLogId ", new { Content = newText, EventLogId = eventId });
            }
        }

        public static List<int> GetHelpfulMarksLogIds(int eventLogId, SqlConnection connection = null)
        {
            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection())
                {
                    return GetHelpfulMarksLogIds(eventLogId, sqlc);
                }
            }

            // get the logCommentId
            int logId = connection.Query<int>("SELECT Id " +
                                              "FROM LogCommentEvents " +
                                              "WHERE EventLogId = @eventId", new { eventId = eventLogId }).FirstOrDefault();

            return connection.Query<int>("SELECT EventLogId " +
                                      "FROM HelpfulMarkGivenEvents " +
                                      "WHERE LogCommentEventId = @logId",
                                      new { logId }).ToList();

        }

        /// <summary>
        /// Inserts HelpfulMarkGivenEvent
        /// </summary>
        /// <returns>Number of helpfulMarks associated with the logComment being marked</returns>
        public static int MarkLogCommentHelpful(int eventLogId, int markerId, SqlConnection connection = null)
        {
            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection())
                {
                    return MarkLogCommentHelpful(eventLogId, markerId, sqlc);
                }
            }

            var eventIds = GetHelpfulMarksLogIds(eventLogId, connection);

            // get the Log Comment ID
            int logId = connection.Query<int>("SELECT Id " +
                                              "FROM LogCommentEvents " +
                                              "WHERE EventLogId = @eventId", new { eventId = eventLogId }).FirstOrDefault();

            int logSenderId = connection.Query<int>("SELECT SenderId " +
                                                    "FROM EventLogs " +
                                                    "WHERE Id = @logCommentId ", new { logCommentId = logId }).FirstOrDefault();

            // do not allow user to possibly mark their own comment as helpful
            if (logSenderId == markerId)
            {
                return eventIds.Count;
            }

            // get the Source Event Log ID
            int sourceLogId = connection.Query<int>("SELECT SourceEventLogId " +
                                                    "From LogCommentEvents " +
                                                    "WHERE EventLogId = @eventId", new { eventId = eventLogId }).FirstOrDefault();
            // get the Source Event Log visibility groups
            string sourceLogEventVisibilityGroups = connection.Query<string>("SELECT ISNULL(EventVisibilityGroups, '') " +
                                                    "From EventLogs " +
                                                    "WHERE Id = @eventId", new { eventId = sourceLogId }).FirstOrDefault();
            // get the Source Event Log visible to list
            string sourceLogEventVisibleTo = connection.Query<string>("SELECT ISNULL(EventVisibleTo, '') " +
                                                    "From EventLogs " +
                                                    "WHERE Id = @eventId", new { eventId = sourceLogId }).FirstOrDefault();

            // get the Source Event Course ID
            int? sourceCourseId = null;

            try
            {
                sourceCourseId = connection.Query<int>("SELECT CourseId " +
                                                       "FROM EventLogs " +
                                                       "WHERE Id = @SourceEventId", new { SourceEventId = sourceLogId }).FirstOrDefault();
            }
            catch (NullReferenceException ex)
            {
                // do nothing
            }

            List<ActivityEvent> eventLogs =
                connection.Query<ActivityEvent>("SELECT Id AS EventLogId, EventTypeId AS EventId, EventDate, DateReceived, SenderId, CourseId, SolutionName, IsDeleted " +
                                                "FROM EventLogs " +
                                                "WHERE Id IN @logId", new { logId = eventIds }).ToList();



            ActivityEvent senderEvent = eventLogs.Find(x => x.SenderId == markerId);


            // user clicked again, remove the MarkHelpfulCommentEvent
            if (senderEvent != null)
            {
                connection.Execute("DELETE FROM HelpfulMarkGivenEvents " +
                                   "WHERE EventLogId = @senderEventId", new { senderEventId = senderEvent.EventLogId });

                connection.Execute("DELETE FROM EventLogs " +
                   "WHERE Id = @senderEventId", new { senderEventId = senderEvent.EventLogId });
                // do not execute insert string, just return number of marks
                return eventIds.Count - 1;
            }

            try
            {
                HelpfulMarkGivenEvent h;
                if (sourceCourseId == null)
                {
                    h = new HelpfulMarkGivenEvent()
                    {
                        LogCommentEventId = logId,
                        SenderId = markerId,
                        SolutionName = "",
                        EventVisibilityGroups = sourceLogEventVisibilityGroups,
                        EventVisibleTo = sourceLogEventVisibleTo
                    };
                }
                else
                {
                    h = new HelpfulMarkGivenEvent()
                    {
                        LogCommentEventId = logId,
                        SenderId = markerId,
                        CourseId = sourceCourseId,
                        SolutionName = "",
                        EventVisibilityGroups = sourceLogEventVisibilityGroups,
                        EventVisibleTo = sourceLogEventVisibleTo
                    };
                }


                using (var cmd = h.GetInsertCommand())
                {
                    cmd.Connection = connection;
                    connection.Open();
                    cmd.ExecuteScalar();
                    connection.Close();
                    return eventIds.Count + 1;
                }
            }
            catch (Exception ex)
            {
                return eventIds.Count;
            }
        }

        /// <summary>
        /// Updates the FeedPostEventFlags db.
        /// </summary>
        /// <param name="eventLogId"> The event's identification number. In the EventLogs db, the column is just "Id". In
        /// FeedPostEventFlags db, the column is FeedPostEventId. </param>
        /// <param name="isResolved"> Boolean that determines whether the event has been marked as resolve or not. In
        /// FeedPostEventFlags db, the column is MarkedResolved. </param>
        /// courtney-snyder
        public static void MarkFeedPostResolved(int eventLogId, bool isResolved)
        {
            try
            {
                using (var sqlConnection = new SqlConnection(StringConstants.ConnectionString))
                {
                    sqlConnection.Open();

                    string query = "SELECT * FROM FeedPostEventFlags WHERE FeedPostEventId = @EventLogId ";
                    string updateQuery = "UPDATE FeedPostEventFlags SET MarkedResolved = @IsResolved WHERE FeedPostEventId = @EventLogId ";
                    string insertQuery = "INSERT INTO FeedPostEventFlags (FeedPostEventId,MarkedResolved) VALUES (@EventLogId, @IsResolved) ";

                    var result = sqlConnection.Query(query, new { EventLogId = eventLogId, IsResolved = isResolved }).FirstOrDefault();

                    //If the post is already in the table, update the MarkResolved value
                    if (result != null)
                    {
                        sqlConnection.Execute(updateQuery, new { EventLogId = eventLogId, IsResolved = isResolved });
                    }

                    //Otherwise, add the post to the table with the correct MarkResolved value
                    else
                    {
                        sqlConnection.Execute(insertQuery, new { EventLogId = eventLogId, IsResolved = isResolved });
                    }
                }
            }
            
            catch (Exception e)
            {
                //Nothing for now
            }
        }

        /// <returns> Returns a list of post IDs marked as "Resolved" from the FeedPostEventFlags db </returns>
        /// courtney-snyder
        public static List<int> GetResolvedPostIds()
        {
            using (var sqlConnection = new SqlConnection(StringConstants.ConnectionString))
            {
                sqlConnection.Open();
                
                //Get all Resolved posts
                string query = "SELECT FeedPostEventId FROM FeedPostEventFlags WHERE MarkedResolved = 1 ";
                List<int> resolvedPosts = sqlConnection.Query<int>(query).ToList();
                return resolvedPosts;
            }
        }

        /// <summary>
        /// Takes a list of post event IDs and returns a list of those post event IDs that are "Resolved".
        /// </summary>
        /// <param name="postEventIds"> An string list of post event ids. </param>
        /// <returns> Returns a list of post IDs marked as "Resolved" from the FeedPostEventFlags db </returns>
        /// courtney-snyder
        public static IEnumerable<int> GetResolvedPostIds(List<int> postEventIds)
        {
            using (var sqlConnection = new SqlConnection(StringConstants.ConnectionString))
            {
                sqlConnection.Open();
                //Get Resolved posts from the list of postEventIds
                string query = "SELECT FeedPostEventId FROM FeedPostEventFlags WHERE FeedPostEventId IN @EventList AND MarkedResolved = 1 ";
                IEnumerable<int> result = sqlConnection.Query<int>(query, new { EventList = postEventIds }).ToList();
                return result;
            }
        }

        /// <returns>
        /// Returns a dictionary of post IDs (key) and senders (value) marked as "Resolved" from the FeedPostEventFlags db
        /// </returns>
        /// courtney-snyder
        public static Dictionary<int, int> GetResolvedPostIdsAndSenderIds()
        {
            using (var sqlConnection = new SqlConnection(StringConstants.ConnectionString))
            {
                sqlConnection.Open();

                //Get all Resolved posts
                string query = "SELECT * FROM FeedPostEventFlags WHERE MarkedResolved = 1 ";
                var resolvedPosts = sqlConnection.Query<int>(query).ToList();
                Dictionary<int, int> returnDict = new Dictionary<int, int>();

                return returnDict;
            }
        }

        /// <returns>
        /// Returns a dictionary of post IDs (key) and senders (value) marked as "Resolved" from the FeedPostEventFlags db
        /// </returns>
        /// <param name="postEventIds"> An int list of post event ids. </param>
        /// courtney-snyder
        public static Dictionary<int, int> GetResolvedPostIdsAndSenderIds(List<int> postEventIds)
        {
            using (var sqlConnection = new SqlConnection(StringConstants.ConnectionString))
            {
                sqlConnection.Open();

                //Get all Resolved posts
                //string query = "SELECT * FROM FeedPostEventFlags WHERE MarkedResolved = 1 ";
                //string query = "SELECT FeedPostEventFlags.FeedPostEventId, EventLogs.SenderId " +
                //               "FROM FeedPostEventFlags " +
                //               "INNER JOIN EventLogs " +
                //               "ON FeedPostEventFlags.FeedPostEventId = EventLogs.Id " +
                //               "WHERE FeedPostEventFlags.FeedPostEventId IN @postEventIds " +
                //               "AND FeedPostEventFlags.MarkedResolved = 1 " ;
                string query = "SELECT FeedPostEventFlags.FeedPostEventId, EventLogs.SenderId " +
                               "FROM FeedPostEventFlags " +
                               "INNER JOIN EventLogs " +
                               "ON FeedPostEventId = EventLogs.Id " +
                               "WHERE FeedPostEventFlags.FeedPostEventId IN @EventIdList " +
                               "AND FeedPostEventFlags.MarkedResolved = 1 ";
                //string temp = "SELECT * FROM LogCommentEvents l INNER JOIN EventLogs e ON l.EventLogId = e.Id WHERE (IsDeleted IS NULL OR IsDeleted = 0) AND e.SenderId = @uid ORDER BY e.EventDate DESC";
                var resolvedPosts = sqlConnection.Query(query, new { EventIdList = postEventIds } ).ToList();
                Dictionary<int, int> postIdSenderIdDict = new Dictionary<int, int>();
                foreach (var r in resolvedPosts)
                {
                    int key = r.FeedPostEventId;
                    int value = r.SenderId;
                    postIdSenderIdDict.Add(key, value);
                }

                return postIdSenderIdDict;
            }
        }

        /// <summary>
        /// Takes the event ID and returns true (is Resolved) or false (is not Resolved).
        /// </summary>
        /// <param name="eventId"> The post's event ID. </param>
        /// <returns> Returns true or false. </returns>
        /// courtney-snyder
        public static bool IsPostResolved(int eventId)
        {
            using (var sqlConnection = new SqlConnection(StringConstants.ConnectionString))
            {
                sqlConnection.Open();

                //Get the post with the given eventId and make sure it is marked as Resolved
                string query = "SELECT * FROM FeedPostEventFlags WHERE FeedPostEventId = @EventLogId AND MarkedResolved = 1 ";
                var result = sqlConnection.Query(query, new { EventLogId = eventId, IsResolved = 1 }).FirstOrDefault();
                //If the query gives a result, the post with that eventId is Resolved
                if (result != null)
                {
                    return true;
                }
                //Otherwise, that post is either not in the database or not resolved
                return false;
            }
        }


        /// <summary>
        /// Finds all occurrences of the eventID in the FeedPostLikes table and returns the count.
        /// </summary>
        /// <param name="eventId"></param>
        /// <returns></returns>
        /// courtney-snyder
        public static int GetPostLikeCount (int eventId)
        {
            using (var sqlConnection = new SqlConnection(StringConstants.ConnectionString))
            {
                sqlConnection.Open();

                //Get the post with the given eventId
                string query = "SELECT * FROM FeedPostLikes WHERE EventLogId = @EventId ";
                var result = sqlConnection.Query(query, new { EventId = eventId }).ToList();
                //If the query gives a result, get the number of elements in the list
                if (result != null)
                {
                    return result.Count;
                }
                //Otherwise, that post has no likes
                return 0;
            }
        }

        /// <summary>
        /// Finds all occurrences of the eventIDs in the FeedPostLikes table and returns the counts.
        /// </summary>
        /// <param name="eventIds"> A list of visible feed item eventIds </param>
        /// <returns> A dictionary of the feed items (key) with their respective like amounts (value) </returns>
        /// courtney-snyder
        public static Dictionary<int, int> GetPostLikeCount(List<int> eventIds)
        {
            using (var sqlConnection = new SqlConnection(StringConstants.ConnectionString))
            {
                sqlConnection.Open();

                //Get the posts
                string query = "SELECT * FROM FeedPostLikes WHERE EventLogId IN @EventId ";
                var result = sqlConnection.Query(query, new { EventId = eventIds }).ToList();
                Dictionary<int, int> eventIdsAndLikes = new Dictionary<int, int>();
                //Add all eventIds to the dictionary and initialize to 0 likes
                foreach (int eventId in eventIds)
                {
                    eventIdsAndLikes.Add(eventId, 0);
                }
                //If the query gives a result, add each result (like) to the dictionary
                if (result != null)
                {
                    foreach (var r in result)
                    {
                        //Increment number of likes for that event ID
                        eventIdsAndLikes[r.EventLogId]++;
                    }
                }
                return eventIdsAndLikes;
            }
        }

        /// <summary>
        /// Checks to see if a user has already liked a post.
        /// </summary>
        /// <param name="eventId"></param>
        /// <param name="senderId"></param>
        /// <returns> True (has been liked by that user) or False (has not). </returns>
        /// courtney-snyder
        public static bool IsPostLikedByUser(int eventId, int senderId)
        {
            using (var sqlConnection = new SqlConnection(StringConstants.ConnectionString))
            {
                sqlConnection.Open();

                //Get the post with the given eventId and check if the user who clicked "Like" (senderId) has already liked the post
                string query = "SELECT * FROM FeedPostLikes WHERE EventLogId = @InputEventLogId AND UserProfileId = @InputSenderId ";
                var result = sqlConnection.Query(query, new { InputEventLogId = eventId, InputSenderId = senderId }).FirstOrDefault();
                //If the query gives a result, that user has liked the post, so return true
                if (result != null)
                {
                    return true;
                }
                //Otherwise, that post has not been liked by that user
                return false;
            }
        }

        /// <summary>
        /// Checks to see if a user has already liked a post.
        /// </summary>
        /// <param name="eventIds"> A list of visible feed items on the feed </param>
        /// <param name="senderId"> The current user </param>
        /// <returns> A list of eventIds that have been liked by the user </returns>
        /// courtney-snyder
        public static List<int> ArePostsLikedByUser(List<int> eventIds, int senderId)
        {
            using (var sqlConnection = new SqlConnection(StringConstants.ConnectionString))
            {
                sqlConnection.Open();

                //Get the posts with the given eventIds list and check if the user has liked any of the visible posts
                string query = "SELECT EventLogId FROM FeedPostLikes WHERE EventLogId IN @InputEventLogId AND UserProfileId = @InputSenderId ";
                var result = sqlConnection.Query(query, new { InputEventLogId = eventIds, InputSenderId = senderId }).ToList();
                List<int> eventIdAndLikeStatus = new List<int>();
                //If the query gives any results, that user has liked a post
                if (result != null)
                {
                    foreach(var r in result)
                    {
                        var seeWhatRIs = r;
                        eventIdAndLikeStatus.Add(r.EventLogId);
                    }
                    //Add each query result into the dictionary
                    return eventIdAndLikeStatus;
                }
                //Otherwise, that post has not been liked by that user
                return null;
            }
        }

        /// <summary>
        /// Updates the number of likes a post has by either adding a new row (like) or removing an existing column (unlike)
        /// </summary>
        /// <param name="eventId"> Item to be affected </param>
        /// <param name="senderId"> Person liking or unliking </param>
        /// courtney-snyder
        public static void UpdatePostItemLikeCount(int eventId, int senderId)
        {
            using (var sqlConnection = new SqlConnection(StringConstants.ConnectionString))
            {
                sqlConnection.Open();

                string query = "SELECT * FROM FeedPostLikes WHERE EventLogId = @EventId AND UserProfileId = @SenderId ";
                string deleteQuery = "DELETE FROM FeedPostLikes WHERE EventLogId = @EventId AND UserProfileId = @SenderId ";
                string insertQuery = "INSERT INTO FeedPostLikes (EventLogId, UserProfileId) VALUES (@EventId, @SenderId) ";

                var result = sqlConnection.Query(query, new { EventId = eventId, SenderId = senderId }).FirstOrDefault();

                //If the user has not liked the event, add a row to the table
                if (result == null)
                {
                    sqlConnection.Execute(insertQuery, new { EventId = eventId, SenderId = senderId });
                }
                //Otherwise the user has already liked this event and it is an "Unlike", so remove the like from the db
                else
                {
                    sqlConnection.Execute(deleteQuery, new { EventId = eventId, SenderId = senderId });
                }
            }
        }

        public static int GetHelpfulMarkFeedSourceId(int helpfulMarkId, SqlConnection connection = null)
        {
            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection())
                {
                    return GetHelpfulMarkFeedSourceId(helpfulMarkId, sqlc);
                }
            }

            return connection.Query<int>("SELECT SourceEventLogId " +
                                         "FROM LogCommentEvents " +
                                         "WHERE Id = (SELECT LogCommentEventId " +
                                         "FROM HelpfulMarkGivenEvents " +
                                         "WHERE EventLogId = @helpfulMarkId)", new { helpfulMarkId }).SingleOrDefault();
        }

        public static bool UserMarkedLog(int userId, int logCommentId, SqlConnection connection = null)
        {
            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection())
                {
                    return UserMarkedLog(userId, logCommentId, sqlc);
                }
            }

            // find the log id
            int logId = connection.Query<int>("SELECT Id " +
                                              "FROM LogCommentEvents " +
                                              "WHERE EventLogId = @logCommentId", new { logCommentId }).SingleOrDefault();

            // find eventlogids
            List<int> helpEvents = connection.Query<int>("SELECT EventLogId " +
                                                         "FROM HelpfulMarkGivenEvents " +
                                                         "WHERE LogCommentEventId = @logId", new { logId }).ToList();

            // find the senders
            List<int> senders = connection.Query<int>("SELECT SenderId " +
                                                   "FROM EventLogs " +
                                                   "WHERE Id IN @helpEvents", new { helpEvents }).ToList();

            return senders.Contains(userId);

        }

        public static Dictionary<int, bool> DictionaryOfMarkedLogs(int userId, List<int> logCommentIds, SqlConnection connection = null)
        {
            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection())
                {
                    return DictionaryOfMarkedLogs(userId, logCommentIds, sqlc);
                }
            }

            Dictionary<int, bool> senderMarkedComment = new Dictionary<int, bool>();
            // find the log id
            List<int> logIds = connection.Query<int>("SELECT Id " +
                                                     "FROM LogCommentEvents " +
                                                     "WHERE EventLogId IN @logCommentIds", new { logCommentIds }).ToList();


            foreach (int logId in logIds)
            {
                // find eventlogids
                List<int> helpEvents = connection.Query<int>("SELECT EventLogId " +
                                             "FROM HelpfulMarkGivenEvents " +
                                             "WHERE LogCommentEventId = @logId", new { logId }).ToList();

                // find the senders
                List<int> senders = connection.Query<int>("SELECT SenderId " +
                                                       "FROM EventLogs " +
                                                       "WHERE Id IN @helpEvents", new { helpEvents }).ToList();

                if (senders.Contains(userId))
                {
                    senderMarkedComment[logId] = true;
                }
                else
                {
                    senderMarkedComment[logId] = false;
                }


            }

            return senderMarkedComment;
        }

        public static IEnumerable<ActivityEvent> GetActivityEventsFromId(int userId, SqlConnection connection = null)
        {
            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection())
                {
                    return GetActivityEventsFromId(userId, sqlc);
                }
            }

            return connection.Query<ActivityEvent>("SELECT Id AS EventLogId, EventTypeId AS EventId, EventDate, DateReceived, SenderId, CourseId, SolutionName, IsDeleted " +
                                                "FROM EventLogs " +
                                                "WHERE SenderId = @id " +
                                                "AND (EventTypeId = 1 OR " +    //AskForHelp
                                                "EventTypeId = 7 OR " +          //FeedPost
                                                "EventTypeId = 8 OR " +         //MarkHelpful
                                                "EventTypeId = 9)" +            //LogComment
                                                "AND (IsDeleted = 0 OR IsDeleted IS NULL) ",
                                                new { id = userId }).ToList();
        }

        public static IEnumerable<LogCommentEvent> GetLogCommentEventsFromEventLogIds(IEnumerable<int> ids,
            IEnumerable<ActivityEvent> activityEvents, SqlConnection connection = null)
        {
            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection())
                {
                    return GetLogCommentEventsFromEventLogIds(ids, activityEvents, sqlc);
                }
            }

            LogCommentEvent l = new LogCommentEvent();


            return connection.Query<LogCommentEvent>("SELECT * " +
                                                  "FROM LogCommentEvents " +
                                                  "WHERE EventLogId IN @ids", new { ids }).ToList();
        }

        public static IEnumerable<int> GetUserFeedFromId(int userId, SqlConnection connection = null)
        {
            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection())
                {
                    return GetUserFeedFromId(userId, sqlc);
                }
            }

            // AddEventTypeIdes here as necessary using the syntax below if you want addition events
            return GetActivityEventsFromId(userId, connection).ToList().Select(i => i.EventLogId).ToList();
        }

        public static IEnumerable<LogCommentEvent> GetCommentsForUserID(int userID, SqlConnection connection = null)
        {
            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection()) { return GetCommentsForUserID(userID, sqlc); }
            }

            IEnumerable<LogCommentEvent> comments = connection.Query<LogCommentEvent>("SELECT * FROM LogCommentEvents l INNER JOIN EventLogs e ON l.EventLogId = e.Id WHERE (IsDeleted IS NULL OR IsDeleted = 0) AND e.SenderId = @uid ORDER BY e.EventDate DESC",
                new { uid = userID });
            return comments;
        }

        public static bool LastSubmitGreaterThanMinutesInterval(int eventLogId, int minutes, SqlConnection connection = null)
        {
            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection()) { return LastSubmitGreaterThanMinutesInterval(eventLogId, minutes, sqlc); }
            }

            bool greaterThanInterval = false;

            try
            {
                DateTime newSubmissionTimestamp = connection.Query<DateTime>("SELECT EventDate FROM EventLogs WHERE Id = @eventLogId ",
                                                                    new { eventLogId = eventLogId }).Single();

                DateTime mostRecentSubmissionTimestamp = connection.Query<DateTime>("SELECT TOP 1 EventDate " +
                                                                                        "FROM EventLogs " +
                                                                                        "WHERE EventTypeId = 11 " + //11 is submit event
                                                                                        "AND SenderId = (SELECT SenderId " +
                                                                                                        "FROM EventLogs " +
                                                                                                        "WHERE Id = @eventLogId) " +
                                                                                        "AND CourseId = (SELECT CourseId " +
                                                                                                        "FROM EventLogs " +
                                                                                                        "WHERE Id = @eventLogId) " +
                                                                                        "AND Id != @eventLogId " +
                                                                                        "ORDER BY Id DESC ",
                                                                        new { eventLogId = eventLogId }).Single();

                TimeSpan difference = newSubmissionTimestamp - mostRecentSubmissionTimestamp;
                if (difference.Minutes > minutes)
                {
                    greaterThanInterval = true;
                }
                return greaterThanInterval;
            }
            catch (Exception)
            {
                return false;
            }
        }

        #endregion


        /*** Activity Feed *************************************************************************************************/
        #region ActivityFeed
        public static bool InsertActivityFeedComment(int logID, int senderID, string text, SqlConnection connection = null, bool isAnonymous = false)
        {
            if (connection == null)
            {
                bool result = false;
                using (SqlConnection sqlc = GetNewConnection()) { result = InsertActivityFeedComment(logID, senderID, text, sqlc, isAnonymous); }
                return result;
            }

            try
            {
                // Get the course id of the original post
                int? courseID;

                // nested try catch for when commenting on a post that has a NULL courseID
                try
                {
                    courseID = connection.Query<int>("SELECT ISNULL(CourseId, 0) FROM EventLogs WHERE Id = @id",
                        new { id = logID }).SingleOrDefault();
                    if (courseID == 0)
                    {
                        courseID = null;
                    }
                }
                catch
                {
                    courseID = null;
                }


                LogCommentEvent e = new LogCommentEvent(DateTime.UtcNow)
                {
                    Content = text,
                    SourceEventLogId = logID,
                    SolutionName = "",
                    SenderId = senderID,
                    CourseId = courseID,
                    SourceEvent = GetActivityEvent(logID, connection),
                    IsAnonymous = isAnonymous,
                };
                using (var cmd = e.GetInsertCommand())
                {
                    // this no longer works, need to do cmd.ExecuteScalar() to get the query to run correctly.
                    //connection.Execute(cmd.CommandText, cmd.Parameters);
                    cmd.Connection = connection;

                    connection.Open();
                    cmd.ExecuteScalar();
                    connection.Close();
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static List<MailAddress> GetActivityFeedForwardedEmails(int courseID, SqlConnection connection = null, bool emailToClass = false)
        {
            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection()) { return GetActivityFeedForwardedEmails(courseID, sqlc, emailToClass); }
            }

            string query = "";

            if (emailToClass) //email to all course users
            {
                query = "SELECT DISTINCT u.* FROM UserProfiles u INNER JOIN CourseUsers c ON u.ID = c.UserProfileID WHERE c.AbstractCourseID = @id ";
            }
            else //email to just those that have email forwarding enabled
            {
                query = "SELECT DISTINCT u.* FROM UserProfiles u INNER JOIN CourseUsers c ON u.ID = c.UserProfileID WHERE c.AbstractCourseID = @id AND u.EmailAllActivityPosts = 1";
            }

            IEnumerable<UserProfile> users = connection.Query<UserProfile>(query,
                new { id = courseID });

            List<MailAddress> addresses = new List<MailAddress>(users.Select(u => new MailAddress(u.UserName, u.FullName)));
            return addresses;
        }

        public static List<MailAddress> GetReplyForwardedEmails(int originalPostID, SqlConnection connection = null)
        {
            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection()) { return GetReplyForwardedEmails(originalPostID, sqlc); }
            }

            IEnumerable<UserProfile> users = connection.Query<UserProfile>(
                "SELECT DISTINCT u.* " +
                "FROM UserProfiles u " +
                "JOIN EventLogs e ON u.ID = e.SenderId " +
                "JOIN LogCommentEvents l ON e.Id = l.EventLogId " +
                "WHERE l.SourceEventLogId = @opID AND u.EmailAllActivityPosts = 1",
                new { opID = originalPostID });

            UserProfile originalPoster = GetFeedItemSender(originalPostID, connection);

            List<MailAddress> addresses = new List<MailAddress>(users.Select(u => new MailAddress(u.UserName, u.FullName)));
            if (originalPoster.EmailAllActivityPosts && !users.Any(u => u.ID == originalPoster.ID))
            {
                addresses.Add(new MailAddress(originalPoster.UserName, originalPoster.FullName));
            }

            return addresses;
        }

        public static UserProfile GetFeedItemSender(int postID, SqlConnection connection = null, bool isAnonymous = false)
        {
            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection()) { return GetFeedItemSender(postID, sqlc, isAnonymous); }
            }

            UserProfile sender = connection.Query<UserProfile>(
            "SELECT u.* " +
            "FROM UserProfiles u " +
            "JOIN EventLogs e ON u.ID = e.SenderId " +
            "WHERE e.Id = @id",
            new { id = postID }).SingleOrDefault();

            if (isAnonymous)
            {
                sender.FirstName = "Anonymous";
                sender.LastName = "User";
            }
            return sender;
        }

        /// <summary>
        /// Gets the senderId of the specified postId
        /// </summary>
        /// <param name="postID"> The post in question </param>
        /// <param name="connection"></param>
        /// <returns></returns>
        /// courtney-snyder
        public static int GetFeedItemSenderId(int postID, SqlConnection connection = null)
        {
            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection()) { return GetFeedItemSenderId(postID, sqlc); }
            }

            var sender = connection.Query(
            "SELECT SenderId " +
            "FROM EventLogs " +
            "WHERE Id = @PostId ",
            new { PostId = postID }).FirstOrDefault();

            return sender.SenderId;
        }

        public static bool IsEventDeleted(int postID, SqlConnection connection = null)
        {
            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection()) { return IsEventDeleted(postID, sqlc); }
            }

            bool? isDeleted = connection.Query<bool?>("SELECT IsDeleted FROM EventLogs WHERE Id = @id",
                new { id = postID }).SingleOrDefault();
            return isDeleted.HasValue && isDeleted.Value;
        }

        public static bool AddHashTags(List<string> hashTags)
        {
            // Add each tag in the list to the database (if it doesn't already exist in the database)
            try
            {
                using (var sqlConnection = new SqlConnection(StringConstants.ConnectionString))
                {
                    sqlConnection.Open();

                    string query = "BEGIN IF NOT EXISTS (SELECT Content FROM HashTags WHERE Content = @Content) BEGIN INSERT INTO HashTags values (@Content) END END";

                    // TODO: optimize to run one query inserting all hashtags
                    foreach (string hashTag in hashTags)
                    {
                        sqlConnection.Query<int>(query, new { Content = hashTag });
                    }

                    sqlConnection.Close();

                    return true;
                }
            }
            catch (Exception e)
            {
                //TODO: handle exception logging
                return false; //failure
            }
        }

        public static List<string> GetHashTags()
        {
            try
            {
                using (var sqlConnection = new SqlConnection(StringConstants.ConnectionString))
                {
                    sqlConnection.Open();

                    string query = "";

                    query = "SELECT DISTINCT Content FROM HashTags";

                    List<string> hashTags = sqlConnection.Query<string>(query).ToList();

                    sqlConnection.Close();

                    return hashTags;
                }
            }
            catch (Exception e)
            {
                //TODO: handle exception logging
                return new List<string>(); //failure, return empty list
            }
        }

        #endregion

        /*** Assignments *******************************************************************************************/
        #region Assignments
        public static string GetAssignmentName(int assignmentId = 0, SqlConnection connection = null)
        {
            if (assignmentId == 0)
            {
                return "";
            }

            string assignmentName = "";

            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection())
                {
                    assignmentName = GetAssignmentName(assignmentId, sqlc);
                }
                return assignmentName;
            }

            assignmentName = connection.Query<string>("SELECT ISNULL(AssignmentName, '') FROM Assignments WHERE ID = @assignmentId",
                new { assignmentId = assignmentId }).SingleOrDefault();

            return assignmentName;
        }
        public static List<Assignment> GetObservableAssignments(int CourseID)
        {
            List<Assignment> assignments = new List<Assignment>();
            var sqlConnection = new SqlConnection(StringConstants.ConnectionString);

            string query = "SELECT * FROM AssignmentObserverSettings,Assignments WHERE Assignments.CourseID=@CourseID AND AssignmentObserverSettings.AssignmentID=Assignments.ID AND AssignmentObserverSettings.IsObservable=1";

            assignments = sqlConnection.Query<Assignment>(query, new  {CourseID = CourseID}).ToList();

            return assignments;
        }
        public static bool CheckIfObservable(int assignmentID)
        {
            

            var sqlConnection = new SqlConnection(StringConstants.ConnectionString);

            string query = "SELECT IsObservable From AssignmentObserverSettings WHERE AssignmentID=@AssignmentID";

            List<bool> isOb = sqlConnection.Query<bool>(query, new { AssignmentID = assignmentID }).ToList();

            if(isOb.Count == 0)
            {

                return false;
            }
            else
            {
                
                return isOb[0];
            }
           
        }
        public static void SwitchObservable(int assignmentID)
        {
   
            string query;
            bool isOb = true;
            var sqlConnection = new SqlConnection(StringConstants.ConnectionString);

            query = "SELECT IsObservable FROM AssignmentObserverSettings WHERE AssignmentID=@AssignmentID;";
            var results = sqlConnection.Query(query, new { AssignmentID = assignmentID }).SingleOrDefault();
            
            if(results == null)
            {
                string InsertQuery = "INSERT INTO AssignmentObserverSettings VALUES(@AssignmentID,@isOb);";
                sqlConnection.Execute(InsertQuery, new { AssignmentID = assignmentID, isOb = isOb });
            }
            else if(results.IsObservable)
            {
                query = "UPDATE AssignmentObserverSettings SET IsObservable = 0 WHERE AssignmentID=@AssignmentID";
            }
            else
            {
                query = "UPDATE AssignmentObserverSettings SET IsObservable = 1 WHERE AssignmentID=@AssignmentID";
            }

            sqlConnection.Execute(query, new { AssignmentID = assignmentID });
            
        }
        //ckfrancisco
        //create a dictionary of course assignments name keys and assignment id values
        public static Dictionary<string, int> GetAssignmentDict(int courseId = 0, SqlConnection connection = null)
        {
            if (courseId == 0)
            {
                return null;
            }

            Dictionary<string, int> assignmentDict = new Dictionary<string, int>();

            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection())
                {
                    assignmentDict = GetAssignmentDict(courseId, sqlc);
                }
                return assignmentDict;
            }

            string queryString = "SELECT AssignmentName, ID from Assignments WHERE CourseID = " + courseId.ToString();

            connection.Open();

            using (SqlCommand command = new SqlCommand(queryString, connection))
            {
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    //insert each assignment name and id if the name does not exist in the dictionary
                    while (reader.Read())
                    {
                        if (!assignmentDict.ContainsKey(reader.GetString(0)))
                            assignmentDict.Add(reader.GetString(0), reader.GetInt32(1));
                    }
                }
            }

            return assignmentDict;
        }
        /// <summary>
        /// This runs a query to update a published rubric to make it unpublished 
        /// </summary>
        /// <param name="rubricID"></param>
        /// <returns></returns>
        internal static bool UnPublishRubric(int assignmentID, int rubricID)
        {
            string query;
            bool didUpdate = false;
            var sqlConnection = new SqlConnection(StringConstants.ConnectionString);

            query = "UPDATE RubricEvaluations SET IsPublished=0, DatePublished=NULL WHERE ID=@rubricID AND AssignmentID=@assignmentID ;";

            int temp = sqlConnection.Execute(query, new { rubricID = rubricID, assignmentID = assignmentID }); //temporary int to later conver to bool
            didUpdate = Convert.ToBoolean(temp);
            return didUpdate;
        }
        internal static bool UnPublishAllRubrics(int assignmentID)
        {
            string query;
            bool didUpdate = false; ;
            var sqlConnestion = new SqlConnection(StringConstants.ConnectionString);

            query = "UPDATE RubricEvaluations SET IsPublished=0, DatePublished=NULL WHERE AssignmentID=@assignmentID;";

            int temp = sqlConnestion.Execute(query, new { assignmentID = assignmentID });
            didUpdate = Convert.ToBoolean(temp);
            return didUpdate;
        }

        #endregion

        /// <summary>
        /// Removes notifications for the published rubrics when the instructor unpublishes a rubric.
        /// </summary>
        /// <param name="assignment"></param>
        /// <param name="teamID"></param>
        /// <param name="instructor"></param>
        /// <param name="rubricPublished"></param>
        internal static void RemoveNotifications(Assignment assignment, int teamID, CourseUser instructor, DateTime rubricPublished)
        {
            string query = "";
            string addMinutes;
            string rubricPublishedString;

            string  assignmentName = assignment.AssignmentName;
            int TeamID = teamID;
            int userID = instructor.ID;

            DateTime addMinute = rubricPublished;
            addMinute = addMinute.AddSeconds(7);

            addMinutes = Convert.ToString(addMinute);

            rubricPublishedString = Convert.ToString(rubricPublished);

            rubricPublished = Convert.ToDateTime(rubricPublishedString);

            addMinute = Convert.ToDateTime(addMinutes);

            





            var sqlConnection = new SqlConnection(StringConstants.ConnectionString);

            query = "SELECT CourseUserID FROM TeamMembers WHERE TeamID=@TeamID;";

            var RecipientID = sqlConnection.Query<int>(query, new { TeamID = TeamID }).FirstOrDefault();

            RecipientID = Convert.ToInt32(RecipientID);

            query = "DELETE FROM Notifications WHERE Data LIKE '%@assignmentName%' AND SenderID=@userID AND RecipientID=@RecipientID AND Posted BETWEEN @rubricPublished AND @addMinute;";
            
            var result = sqlConnection.Query(query, new { assignmentName = assignmentName, userID = userID, RecipientID = RecipientID, rubricPublished = rubricPublished, addMinute = addMinute }).FirstOrDefault();

            


        }

        /// <summary>
        /// Removes all notifications from the database for the given assignment. 
        /// </summary>
        /// <param name="assignmentID"></param>
        /// <param name="instructorID"></param>
        internal static void RemoveNotificationsForEntireAssignment(int assignmentID, int instructorID)
        {
            string query = "";
            string assignmentName = GetAssignmentName(assignmentID);

            
            int userID = instructorID;

            var sqlConnection = new SqlConnection(StringConstants.ConnectionString);

            query = "DELETE FROM Notifications WHERE Data LIKE '%@assignmentName%' AND SenderID=@userID;";

            var result = sqlConnection.Query(query, new { assignmentName = assignmentName, userID = userID });
        }
        internal static void UpdateEventVisibleToList(int eventLogId, string updatedVisibilityList, SqlConnection connection = null)
        {
            if (connection == null)
            {
                using (SqlConnection sqlc = GetNewConnection()) { UpdateEventVisibleToList(eventLogId, updatedVisibilityList, sqlc); }
            }
            else
            {
                connection.Query("UPDATE EventLogs " +
                                    "SET EventVisibleTo = @updatedVisibilityList, EventVisibilityGroups = 'Selected Users' " +
                                    "WHERE EventLogs.Id = @eventLogId",
                                    new { eventLogId = eventLogId, updatedVisibilityList = updatedVisibilityList });
            }

            return;
        }
        /// <summary>
        /// Adds a webpage submission to the SubmitEventProperties table that keeps track of where a submission came from
        /// </summary>
        /// <param name="EventLogId"></param>
        internal static void AddToSubmitEventProperties(int EventLogId)
        {
            bool Webpage = true;
            bool Plugin = false;
            string query = "";


            var sqlConnection = new SqlConnection(StringConstants.ConnectionString);

            query = "INSERT INTO SubmitEventProperties(EventLogId,IsWebpageSubmit,IsPluginSubmit) VALUES (@EventLogId,@Webpage,@Plugin);";

            var result = sqlConnection.Query(query, new { EventLogId = EventLogId, Webpage = Webpage, Plugin = Plugin });



        }
        internal static bool InterventionEnabledForCourse(int courseId)
        {
            bool interventionsEnabled = false;
            try
            {
                using (var sqlConnection = new SqlConnection(StringConstants.ConnectionString))
                {
                    sqlConnection.Open();

                    string query = "SELECT * FROM OSBLEInterventionsCourses WHERE CourseId = @CourseId ";

                    var result = sqlConnection.Query(query, new { CourseId = courseId }).SingleOrDefault();

                    if (result != null)
                    {
                        interventionsEnabled = result.InterventionsEnabled;
                    }

                    sqlConnection.Close();
                }
            }
            catch (Exception e)
            {
                return false;
            }

            return interventionsEnabled;
        }

        internal static string GetRoleNameFromCourseAndUserProfileId(int courseId, int userProfileId)
        {
            string roleName = "User";
            try
            {
                using (var sqlConnection = new SqlConnection(StringConstants.ConnectionString))
                {
                    sqlConnection.Open();

                    string query = "SELECT Name FROM AbstractRoles ar INNER JOIN CourseUsers cu ON ar.ID = cu.AbstractRoleID " +
                                   "WHERE cu.AbstractCourseID = @CourseId AND cu.UserProfileID = @UserProfileId ";

                    roleName = sqlConnection.Query<string>(query, new { CourseId = courseId, UserProfileId = userProfileId }).SingleOrDefault();

                    sqlConnection.Close();
                }
            }
            catch (Exception e)
            {
                return "User";
            }
            return roleName;
        }

        internal static bool GetGradebookSectionEditableSettings(int courseId)
        {
            bool sectionsEditable = false;
            try
            {
                using (var sqlConnection = new SqlConnection(StringConstants.ConnectionString))
                {
                    sqlConnection.Open();

                    string query = "SELECT * FROM GradebookSettings WHERE CourseId = @CourseId ";

                    var result = sqlConnection.Query(query, new { CourseId = courseId }).SingleOrDefault();

                    if (result != null)
                    {
                        sectionsEditable = result.SectionsEditable;
                    }   // else we'll just return false

                    sqlConnection.Close();
                }
            }
            catch (Exception e)
            {
                return sectionsEditable;
            }
            return sectionsEditable;
        }

        internal static bool ToggleGradebookSectionsEditable(int courseId)
        {
            bool updateSuccess = false;
            try
            {
                using (var sqlConnection = new SqlConnection(StringConstants.ConnectionString))
                {
                    sqlConnection.Open();

                    string query = "SELECT * FROM GradebookSettings WHERE CourseId = @CourseId ";

                    var result = sqlConnection.Query(query, new { CourseId = courseId }).SingleOrDefault();

                    if (result != null) //we found a match, let's update it.
                    {
                        bool sectionsEditable = result.SectionsEditable;

                        string updateQuery = "UPDATE GradebookSettings SET SectionsEditable = @SectionsEditable WHERE CourseId = @CourseId ";
                        updateSuccess = sqlConnection.Execute(updateQuery, new { CourseId = courseId, SectionsEditable = !sectionsEditable }) != 0; //toggle the sectionsEditable value

                    }
                    else //no match, insert a row... set to true because the default will be false
                    {
                        string insertQuery = "INSERT INTO GradebookSettings (CourseId, SectionsEditable) VALUES (@CourseId, 1) ";
                        updateSuccess = sqlConnection.Execute(insertQuery, new { CourseId = courseId }) != 0;
                    }

                    sqlConnection.Close();
                }
            }
            catch (Exception e)
            {
                return updateSuccess; //failure!
            }
            return updateSuccess;
        }

        internal static bool SetIsProgrammingCourse(int courseId, bool isProgrammingCourse)
        {
            bool updateSuccess = false;
            try
            {
                using (var sqlConnection = new SqlConnection(StringConstants.ConnectionString))
                {
                    sqlConnection.Open();

                    string query = "SELECT * FROM OSBLEInterventionsCourses WHERE CourseId = @CourseId ";

                    var result = sqlConnection.Query(query, new { CourseId = courseId }).SingleOrDefault();

                    if (result != null) //we found a match, let's update it if it's different than the current value
                    {
                        string updateQuery = "UPDATE OSBLEInterventionsCourses SET IsProgrammingCourse = @IsProgrammingCourse WHERE CourseId = @CourseId ";
                        updateSuccess = sqlConnection.Execute(updateQuery, new { CourseId = courseId, IsProgrammingCourse = isProgrammingCourse }) != 0; //toggle the sectionsEditable value

                    }
                    else //no match, insert a row, default to interventions disabled
                    {
                        string insertQuery = "INSERT INTO OSBLEInterventionsCourses (CourseId, InterventionsEnabled, IsProgrammingCourse) VALUES (@CourseId, 0, @IsProgrammingCourse) ";
                        updateSuccess = sqlConnection.Execute(insertQuery, new { CourseId = courseId, IsProgrammingCourse = isProgrammingCourse }) != 0;
                    }

                    sqlConnection.Close();
                }
            }
            catch (Exception e)
            {
                return updateSuccess; //failure!
            }
            return updateSuccess;
        }

        internal static bool GetIsProgrammingCourseSetting(int courseId)
        {
            bool isProgrammingCourse = false;
            try
            {
                using (var sqlConnection = new SqlConnection(StringConstants.ConnectionString))
                {
                    sqlConnection.Open();

                    string query = "SELECT * FROM OSBLEInterventionsCourses WHERE CourseId = @CourseId ";

                    var result = sqlConnection.Query(query, new { CourseId = courseId }).SingleOrDefault();

                    if (result != null) //we found a match
                    {
                        isProgrammingCourse = result.IsProgrammingCourse;
                    }

                    //else no match, leave it false

                    sqlConnection.Close();
                }
            }
            catch (Exception e)
            {
                return isProgrammingCourse; //failure!
            }
            return isProgrammingCourse;
        }

        internal static DateTime GetInterventionLastRefreshTime(int userProfileId)
        {
            DateTime lastRefresh = DateTime.UtcNow;
            try
            {
                using (var sqlConnection = new SqlConnection(StringConstants.ConnectionString))
                {
                    sqlConnection.Open();

                    string query = "SELECT * FROM OSBLEInterventionsStatus WHERE UserProfileId = @UserProfileId ";

                    var result = sqlConnection.Query(query, new { UserProfileId = userProfileId }).SingleOrDefault();

                    if (result != null) //we found a match
                    {
                        lastRefresh = result.LastRefresh;
                    }

                    //else no match, leave it time now

                    sqlConnection.Close();
                }
            }
            catch (Exception e)
            {
                return lastRefresh; //failure!
            }
            return lastRefresh;
        }

        internal static List<Event> RemoveDuplicateEvents(List<Event> events)
        {
            if (events.Count() == 0)
            {
                return events;
            }

            int posterId = events.First().Poster.ID;
            List<Event> newEvents = new List<Event>(events);

            try
            {
                using (SqlConnection sqlConnection = new SqlConnection(StringConstants.ConnectionString))
                {
                    sqlConnection.Open();
                    string query = "SELECT * FROM Events WHERE PosterID = @PosterId ";
                    var results = sqlConnection.Query(query, new { PosterId = posterId });
                    sqlConnection.Close();

                    if (results == null || results.Count() == 0)
                    {
                        return events;
                    }

                    foreach (var item in results)
                    {
                        foreach (Event newEvent in events)
                        {
                            if (item.StartDate == newEvent.StartDate &&
                                item.EndDate == newEvent.EndDate &&
                                item.Title == newEvent.Title &&
                                item.Description == newEvent.Description)
                            {
                                newEvents.Remove(newEvent);
                                break;   
                            }                            
                        }
                    }
                }
            }
            catch (Exception e)
            {
                throw new Exception("Failed to remove duplicate events: ", e);
                return new List<Event>();
            }

            return newEvents;
        }

        internal static bool DeleteCurrentUserEvents(int posterId)
        {
            try
            {
                using (SqlConnection sqlConnection = new SqlConnection(StringConstants.ConnectionString))
                {
                    sqlConnection.Open();
                    string query = "DELETE FROM Events WHERE PosterID = @PosterId AND (Description NOT LIKE '\\[url:Assignment Page%' ESCAPE '\\' OR Description IS NULL) ";
                    sqlConnection.Execute(query, new { PosterId = posterId });
                    sqlConnection.Close();
                }
            }
            catch (Exception e)
            {
                throw new Exception("Error in DeleteCurrentUserEvents(): ", e);
            }
            return true;
        }
    }
}