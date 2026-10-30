// There is one Clícalo per user and session (SIS-003), and every measurement starts Clicalo.exe as a process: two test
// classes running at the same time would find each other's instance, so a start would end with code 0, 2 or 3 before
// its first frame (issue 6: «InstanceSquatted» was the pipe SingleInstanceTests holds on purpose, seen by the start of
// S5StartupTests). xUnit runs the classes of an assembly in parallel unless told otherwise.
[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]
