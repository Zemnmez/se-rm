using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        // This file contains your actual script.
        //
        // You can either keep all your code here, or you can create separate
        // code files to make your program easier to navigate while coding.
        //
        // In order to add a new utility class, right-click on your project, 
        // select 'New' then 'Add Item...'. Now find the 'Space Engineers'
        // category under 'Visual C# Items' on the left hand side, and select
        // 'Utility Class' in the main area. Name it in the box below, and
        // press OK. This utility class will be merged in with your code when
        // deploying your final script.
        //
        // You can also simply create a new utility class manually, you don't
        // have to use the template if you don't want to. Just do so the first
        // time to see what a utility class looks like.
        // 
        // Go to:
        // https://github.com/malware-dev/MDK-SE/wiki/Quick-Introduction-to-Space-Engineers-Ingame-Scripts
        //
        // to learn more about ingame scripts.

        float FontSize = .5F;

        public Program()
        {
            // The constructor, called only once every session and
            // always before any other method is called. Use it to
            // initialize your script. 
            //     
            // The constructor is optional and can be removed if not
            // needed.
            // 
            // It's recommended to set Runtime.UpdateFrequency 
            // here, which will allow your script to run itself without a 
            // timer block.
            Clear();
            Screen().FontSize = FontSize;
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
        }

        public void Save()
        {
            // Called when the program needs to save its state. Use
            // this method to save your state to the Storage field
            // or some other means. 
            // 
            // This method is optional and can be removed if not
            // needed.
        }

        List<string> TextLines = new List<string>();
        StringBuilder letterM = new StringBuilder("M");

        private IMyTextSurface Screen()
        {
            return Me.GetSurface(0);
        }

        private int NumLines()
        {
            var screenSize = Screen().SurfaceSize;

            var MLetterSize = Screen().MeasureStringInPixels(letterM, Screen().Font, FontSize);
            return (int)((screenSize.X / 2) / MLetterSize.X) - 4;
        }

        private void Clear()
        {
            Screen().WriteText("", false);
        }

        private void Write(string what)
        {
            Screen().WriteText(what);
            Screen().ContentType = ContentType.TEXT_AND_IMAGE;
        }

        private void Println(params string[] what)
        {
            TextLines.Add(String.Join("", what));
            while (TextLines.Count > NumLines())
            {
                TextLines.RemoveAt(0);
            }
            Write(String.Join("\n", TextLines));
        }

        private void Log(params string[] what)
        {
            Println(/*"[", DateTime.Now.ToString(), "]",*/ String.Join(" ", what));
        }

        private List<T> BlocksOfType<T>() where T : class
        {
            var l = new List<T>();
            GridTerminalSystem.GetBlocksOfType<T>(l);
            return l;
        }

        private List<IMyDoor> Doors()
        {
            return BlocksOfType<IMyDoor>();
        }

        private List<IMyAirVent> Vents()
        {
            return BlocksOfType<IMyAirVent>();
        }

        enum MainStates
        {
            InitDetectRooms,
            DetectingRooms,
            DoneDetectingRooms
        }

        class Step
        {
            public static Step Noop() {
                var s = new Step();
                s.noop = true;
                return s;
            }
            public static Step Wait(int n)
            {
                var s = new Step();
                s.waitFor = n;
                return s;
            }
            public static Step Next(IEnumerator<Step> next)
            {
                var s = new Step();
                s.next = next;
                return s;
            }
            public int waitFor = 0;
            public IEnumerator<Step> next;
            public bool noop = false;
        }


        public void Main(string argument, UpdateType updateSource)
        {
            // Make sure that `MyMethod()` is not already running
            if (null == stateMachine)
            {
                Log("Starting...");
                Runtime.UpdateFrequency = UpdateFrequency.Update10;
                shouldContinue = true;
                remainingWaitCount = 0;
                var enumerator = Enumerator();
                if (enumerator == null) { Log("WARNING!! enumerator is missing!"); }
                stateMachine = EnumeratorUnroller(Enumerator());
                if (stateMachine == null) { Log("WARNING!! state machine is missing!!"); }
            }


            if (argument.Length > 0)
            {
                Log("Got signal:", argument);
                if (argument.Equals("reboot", StringComparison.OrdinalIgnoreCase))
                {
                    stateMachine.Dispose();
                    stateMachine = EnumeratorUnroller(Enumerator());
                    shouldContinue = true;
                    remainingWaitCount = 0;
                }

                if (argument.Equals("stop", StringComparison.OrdinalIgnoreCase))
                {
                    // Can only do a stop, when `MyMethod()` previously have been started
                    if (null != stateMachine)
                    {
                        // Instruct `MyMethod` that it should stop, the next time it executes its `while(...)` statement
                        shouldContinue = false;
                    }
                }
            }

            // Only when PB is called with `Update10` in `updateSource`
            if (0 == (updateSource & UpdateType.Update10))
            {
                Log("Exited, incorrect update source");
                return;
            }

            // Make sure there actually is a state-machine running
            if (null == stateMachine)
            {

                Log("Exited, no state machine.");
                return;

            }
            // Reduce the 'remaining counter' - e.g. the "sleep clock"
            remainingWaitCount -= 1;

            // When 'remaining counter' has reached zero (or below), then we are ready to continue the next part of `MyMethod()`
            if (!(remainingWaitCount <= 0))
            {

                return;
            }

            try {
            // `MoveNext()` executes next part of `MyMethod()` until the next `yield ...` statement
            // But `MoveNext()` will also return `false` when `MyMethod()` has nothing more to do
            if (false == stateMachine.MoveNext())
            {
                Log("Script finished.");
                stateMachine.Dispose(); // Important to clean up!
                stateMachine = null; // Important to clean up!
                Runtime.UpdateFrequency = UpdateFrequency.None;
            }
            else
            {
                // Take the `yield return ...`-value and store it in our 'remaining counter'
                remainingWaitCount = stateMachine.Current;
            }
                }
            catch(Exception ex)
            {
                Log("FATAL: ", ex.ToString());
                throw ex;
            }



        }

        int remainingWaitCount;
        bool shouldContinue;
        IEnumerator<int> stateMachine = null;

        IEnumerator<int> EnumeratorUnroller(IEnumerator<Step> e)
        {
            //Log(e.ToString());
            while (e.MoveNext())
            {
                if (e.Current.next != null)
                {
                    var unroller = EnumeratorUnroller(e.Current.next);
                    while (unroller.MoveNext())
                    {
                        yield return unroller.Current;
                    }
                    unroller.Dispose();
                }
                else if (e.Current.waitFor != 0)
                {
                    yield return e.Current.waitFor;
                }
                else if (e.Current.noop)
                {
                    continue;
                }
                else { throw new Exception("Nonsensical step command issued."); }
            }
            e.Dispose();
        }

        IEnumerator<Step> Enumerator()
        {
            var s2 = Do();
            while (shouldContinue)
            {
                s2.MoveNext();
                yield return s2.Current;
            }
            s2.Dispose();
            Log("Stopping Enumerator, due to shouldContinue=false (or a break statement).");
        }

        IEnumerator<Step> Do()
        {
            Log(" == Room Management System == ");
            var state = new SearchState();
            yield return Step.Next(LocateRooms(state));

        }

        IEnumerator<Step> CloseAllDoors()
        {
            Log("Closing all doors...");
            Doors().ForEach(door => door.CloseDoor());
            Log("Waiting for doors to be closed...");
            while (Doors().Exists(v => v.Status != DoorStatus.Closed))
            {
                var openDoors = Doors().Where(d => d.Status == DoorStatus.Opening);
                foreach (IMyDoor door in openDoors) { door.CloseDoor();  Log("Still waiting for", door.CustomName); }
                
                yield return Step.Wait(1);
            }
            Log("All doors closed.");
        }

        IEnumerator<Step> OpenDoor(IMyDoor door)
        {
            Log("Opening door", door.CustomName);
            door.OpenDoor();
            while (door.Status != DoorStatus.Open)
            {
                yield return Step.Wait(1);
            }
            Log("Door", door.CustomName, "is now open");
        }

        IEnumerator<Step> TestLines()
        {
            int end = NumLines();
            Log("Detected", NumLines().ToString(), "rows:");
            for (int i = 0; i < end; i++)
            {
                Log("row:", i.ToString());
                yield return Step.Wait(1);
            }
            Log("Row calibration complete.");
    
        }

        IEnumerator<Step> RemoveVentsThatNeverPressurize(List<IMyAirVent> vents)
        {
            Log("Removing vents that never pressurize");
            yield return Step.Next(CloseAllDoors());

       
            vents.RemoveAll(v =>
            {
                if (!v.CanPressurize)
                {
                    Log("Dropped vent", v.CustomName, "because it never pressurizes.");
                }
                return !v.CanPressurize;
            });
        }

        class Area { }

        class Outside : Area { }

        class Room : Area
        {
            public List<IMyDoor> PathTo;
            public bool IsDeadEnd; 
            public List<IMyAirVent> Vents;
            
            public string Name()
            {
                var namedRoom = Vents.Find(vent => vent.CustomName.Contains("[Room]"));
                if (namedRoom != null) return namedRoom.CustomName;
                return Vents.First().CustomName;
            }

            public bool SameAs(Room r)
            {
                return Vents.TrueForAll(v => r.Vents.Contains(v));
            }
        }


        class SearchState
        {
            public List<Room> Rooms;
            public SearchState()
            {
                Rooms = new List<Room>();
            }

            public IEnumerable<IMyDoor> DoorsKnownToConnectToARoom()
            {
                return Rooms.SelectMany(rm => rm.PathTo).Distinct();
            }
        }

        IEnumerator<Step> OpenDoors(IEnumerable<IMyDoor> doors)
        {
            foreach(var door in doors)
            {
                door.OpenDoor();
            }

            while(doors.ToList().Exists(door => door.Status != DoorStatus.Open))
            {
                yield return Step.Wait(1);
                Log("Waiting for doors to be open:", String.Join(", ", doors.Where(door => door.Status != DoorStatus.Open).Select(door => door.CustomName)));
            }

        }

        IEnumerator<Step> LocateRooms(SearchState state)
        {
            Log("Locating all rooms...");

            var ventsNotInRooms = BlocksOfType<IMyAirVent>();
            Log("Detected", ventsNotInRooms.Count.ToString(), "vents");

            Log("Removing vents that never pressurize");
            yield return Step.Next(CloseAllDoors());

            Log("Vents that never pressurize cannot be in a room.");
       
            ventsNotInRooms.RemoveAll(v =>
            {
                if (!v.CanPressurize)
                {
                    Log("Dropped vent", v.CustomName, "because it never pressurizes.");
                }
                return !v.CanPressurize;
            });

            var doors = BlocksOfType<IMyDoor>();

            Log("Detected", doors.Count.ToString(), "doors on ship", Me.CubeGrid.CustomName);

            List<IMyDoor> uncheckedDoors = doors.Where(door => !state.DoorsKnownToConnectToARoom().Contains(door)).ToList();

            Log("Locating root rooms (rooms that are exposed to outside)");
            Log("Testing", uncheckedDoors.Count.ToString(), "doors");

            var doorsToRemove = new List<IMyDoor>();
            foreach (IMyDoor door in uncheckedDoors)
            {
                Log("Closing all the doors to test door", door.CustomName);
                yield return Step.Next(CloseAllDoors());
                yield return Step.Next(OpenDoor(door));

                var roomVents = ventsNotInRooms.FindAll(v => !v.CanPressurize);
                if (roomVents.Count < 1)
                {
                    Log("Opening only door", door.CustomName, "did not result in a room being depressurized.");
                    continue;
                }


                Log("Opening only door", door.CustomName, "did result in a room being depressurized.");
                Log("Opening door", door.CustomName, "revealed a room containing the set of vents:", String.Join(", ", roomVents.Select(v => v.CustomName)));

                var newRoom = new Room();
                newRoom.Vents = roomVents.ToList();
                ventsNotInRooms = ventsNotInRooms.Except(roomVents).ToList();
                doorsToRemove.Add(door);
                newRoom.PathTo = new List<IMyDoor>();
                newRoom.PathTo.Add(door);
                newRoom.IsDeadEnd = false;
                state.Rooms.Add(newRoom);


                Log("Opening door", door.CustomName, "revealed root room", newRoom.Name());
            }
            uncheckedDoors = uncheckedDoors.Except(doorsToRemove).ToList();

            Log("After finding root rooms, there are", uncheckedDoors.Count.ToString(), "doors left and", ventsNotInRooms.Count.ToString(), "vents left.");

            for (; uncheckedDoors.Count > 0 && state.Rooms.Exists(rm => !rm.IsDeadEnd) && ventsNotInRooms.Count > 0; uncheckedDoors = doors.Where(door => state.DoorsKnownToConnectToARoom().ToList().Exists(d2 => d2.EntityId == door.EntityId)).ToList()) {
                Log("Still to check: ", uncheckedDoors.Count.ToString(), "doors.");

                var newRooms = new List<Room>();
                foreach(var room in state.Rooms.Where(rm => !rm.IsDeadEnd))
                {
                    var doorsThatNowHaveRooms = new List<IMyDoor>();
                    foreach (var door in uncheckedDoors)
                    {
                        Log("Closing all doors to check room", room.Name(), "with door", door.CustomName);
                        yield return Step.Next(CloseAllDoors());
                        Log("Opening all doors to", room.Name());
                        yield return Step.Next(OpenDoors(room.PathTo));
                        Log("Opening door", door.CustomName, "to see if it reveals a room...");
                        yield return Step.Next(OpenDoor(door));

                        var roomVents = ventsNotInRooms.FindAll(vent => !vent.CanPressurize);

                        if (roomVents.Count == 0)
                        {
                            Log("Opening door", door.CustomName, "did not find a room.");
                            continue;
                        }

                        Log("Opening door", door.CustomName, "did result in a room being depressurized.");
                        Log("Opening door", door.CustomName, "revealed a room containing the set of vents:", String.Join(", ", roomVents.Select(v => v.ToString())));

                        var newRoom = new Room();
                        newRoom.Vents = roomVents.ToList();
                        ventsNotInRooms = ventsNotInRooms.Except(roomVents).ToList();
                        doorsThatNowHaveRooms.Add(door);
                        newRoom.PathTo = new List<IMyDoor>();
                        newRoom.PathTo.Add(door);
                        newRoom.IsDeadEnd = false;
                        newRooms.Add(newRoom);
                    }
                    uncheckedDoors = uncheckedDoors.Except(doorsThatNowHaveRooms).ToList();

                    // all doors were checked. room is a dead end.
                    room.IsDeadEnd = true;
                }
                newRooms.ForEach(rm => state.Rooms.Add(rm));


            }

            Log("Search complete. There are", state.Rooms.Count.ToString(), "rooms, as follows:");
            state.Rooms.ForEach(room => Log(room.Name()));
        }




    }
}
