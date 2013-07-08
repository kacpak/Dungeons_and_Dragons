﻿using Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;

namespace DnDServer
{
    [ServiceBehavior(ConcurrencyMode = ConcurrencyMode.Single, InstanceContextMode = InstanceContextMode.Single)]
    class ServerImplementation : IService
    {
        private Dictionary<IClient, User> _users = new Dictionary<IClient, User>();
        private ServerMainWindow window;
        private String hostname = "localhost", database = "dnd", dbUser = "dnd", dbPassword = "pass";
        private DBConnect db;

        public ServerImplementation(ServerMainWindow window) {
            this.window = window;
            db = new DBConnect(hostname, database, dbUser, dbPassword);
        }

        public User User {
            get {
                var connection = OperationContext.Current.GetCallbackChannel<IClient>();
                //
                //TODO Select from db
                //
                //
                return _users.Where(u => u.Key == connection).ToArray()[0].Value;
            }
        }
        public User[] LoggedInUsers {
            get {
                var connection = OperationContext.Current.GetCallbackChannel<IClient>();
                if (_users.Any(_ => _.Key == connection))
                    return _users.Values.ToArray();
                else
                    return new List<User>().ToArray();
            }
        }

        #region Login/Logout/Disconnect
        public Boolean Login(String userName, String userPass) {
            var dbUser = db.Select("`users`", "`login`='" + userName + "' AND `pass`='" + userPass + "'");

            if (dbUser.Count > 0) {
                var connection = OperationContext.Current.GetCallbackChannel<IClient>();
                var user = new User { UserName = userName, LogInTime = DateTime.Now };

                // Remove duplicate users (in case of client crash/unsuccesful logout)
                foreach (var _user in _users)
                    if (_user.Value.UserName == userName)
                        _users.Remove(_user.Key);

                // Add connection
                _users[connection] = user;

                // Succesful login
                window.Message(user.UserName + " logged in.");
                return true;

            } else return false;
        }
        public void Logout() {
            var connection = OperationContext.Current.GetCallbackChannel<IClient>();
            User user;
            if (_users.TryGetValue(connection, out user)) {
                window.Message(user.UserName + " logged out.");
                _users.Remove(connection);
            }
        }
        public void Disconnect() {
            try {
                foreach (var _user in _users.Keys)
                    _user.Disconnect();
            } catch (Exception e) { throw e; }
        }
        #endregion

        public void SendGlobalMessage(String message) {
            var connection = OperationContext.Current.GetCallbackChannel<IClient>();
            User user;
            if (!_users.TryGetValue(connection, out user))
                return;

            foreach (var otherConnection in _users.Keys) {
                if (otherConnection == connection)
                    continue;
                try {
                    otherConnection.ReceiveMessage(message);
                } catch (Exception) { }
            }
        }
    }
}
