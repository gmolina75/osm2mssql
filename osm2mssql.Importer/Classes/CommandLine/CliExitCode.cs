namespace osm2mssql.Importer.Classes.CommandLine
{
    internal enum CliExitCode
    {
        Success = 0,
        InvalidArguments = 1,
        ConnectionError = 2,
        DatabaseExistsWithoutReplace = 3,
        ImportFailed = 4,
        InvalidInputFile = 5
    }
}
