using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NC.Nextion
{
    /// <summary>
    /// Simple State Machine to handle conversation with nextion device
    /// </summary>
    public class NextionSession : IDisposable
    {
        /// <summary>
        /// Current state
        /// </summary>
        public State CurrentState { get; private set; }

        /// <summary>
        /// Represents a function to handle incoming nextion response
        /// as well as
        /// </summary>
        /// <param name="response"></param>
        /// <returns></returns>
        public delegate string HandleResponse(NextionResponse response);

        private Dictionary<string, State> _States = new();
        private IDisposable _Connection;
        private ManualResetEvent _WaitForEnd = new(false);

        /// <summary>
        /// Connect to nextion device, providing simple state machine to handle the reponses
        /// </summary>
        /// <param name="device"></param>
        /// <param name="states"></param>
        public void Begin(NextionDevice device)
        {
            this.EnsuresNotConnected();

            if (_States.Count == 0)
            {
                throw new InvalidOperationException("No State Defined");
            }
                        
            if (this.CurrentState == null)
            {
                throw new InvalidOperationException("First State is not defined");
            }

            _WaitForEnd.Reset();

            _Connection = device.Connect(

                (response) =>
                {
                    var h = this.CurrentState.GetHandler(response.Code);
                    if (h == null)
                    {
                        return; // no handler defined
                    }

                    var next = h(response);
                    if (next != null)
                    {
                        this.CurrentState = _States[next];
                    }
                },

                () =>
                {
                    this.End();
                }
            );
        }

        /// <summary>
        /// End the connection
        /// </summary>
        public void End()
        {
            _Connection?.Dispose();
            _WaitForEnd.Set();
            this.CurrentState = null;
        }

        /// <summary>
        /// Dispose the session
        /// </summary>
        public void Dispose()
        {
            _Connection?.Dispose();
            _WaitForEnd.Set();
        }

        /// <summary>
        /// Wait until the connection has ended
        /// </summary>
        public bool Wait(int timeout = Timeout.Infinite)
        {
            return _WaitForEnd.WaitOne(timeout);
        }

        /// <summary>
        /// Wait until the connection has ended
        /// </summary>
        public async Task<bool> WaitAsync(int timeout = Timeout.Infinite )
        {
            bool result = false;
            await Task.Run(() =>
            {
                result = _WaitForEnd.WaitOne(timeout);
            });

            return result;
        }

        /// <summary>
        /// Create new state
        /// </summary>
        /// <param name="stateName"></param>
        /// <returns></returns>
        public State When( string stateName )
        {
            this.EnsuresNotConnected();

            State existing;
            if (_States.TryGetValue( stateName, out existing))
            {
                return existing;
            }

            _States[stateName] = new State(stateName);
            return _States[stateName];
        }

        /// <summary>
        /// Create new state
        /// </summary>
        /// <param name="stateName"></param>
        /// <returns></returns>
        public State AtFirst()
        {
            this.EnsuresNotConnected();

            this.CurrentState = new State("first");
            return this.CurrentState;
        }

        private void EnsuresNotConnected()
        {
            if (_Connection != null)
            {
                throw new InvalidOperationException("Already Connected");
            }
        }

        public class State
        {
            public string Name { get; set; }

            public State( string name )
            {
                this.Name = name;
            }

            private Dictionary<string, HandleResponse> _Handlers = new();

            /// <summary>
            /// Get Handler for given code
            /// </summary>
            /// <param name="code"></param>
            /// <returns></returns>
            public HandleResponse GetHandler( string code )
            {
                if (code == null)
                {
                    code = "*";
                }

                HandleResponse h;
                if (_Handlers.TryGetValue( code, out h))
                {
                    return h;
                }

                if (_Handlers.TryGetValue("*", out h))
                {
                    return h;
                }

                return null;
            }

            /// <summary>
            /// Create Handler for this State
            /// </summary>
            /// <param name="responseCode"></param>
            /// <param name="handler"></param>
            public State On( string responseCode, Action<NextionResponse> action, string @goto = null)
            {
                _Handlers[responseCode] = (r)=>
                {
                    action(r);
                    return @goto;
                };
                return this;
            }

            /// <summary>
            /// Create Handler for this State
            /// </summary>
            /// <param name="responseCode"></param>
            /// <param name="actionWithGoTo">Function to execute which returns next state</param>
            public State On(string responseCode, HandleResponse actionWithGoTo)
            {
                _Handlers[responseCode] = actionWithGoTo;
                return this;
            }

            /// <summary>
            /// Create Handler for this State
            /// </summary>
            /// <param name="handler"></param>
            /// <returns></returns>
            public State OnAnyCallback(Action<NextionResponse> action, string @goto = null)
            {
                return this.On("*", action, @goto);
            }
        }

    }
}
