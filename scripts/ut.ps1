param(
    [switch]$External
)

$filter = if ($External) { "Category=External" } else { "Category!=External" }

dotnet test "$PSScriptRoot/../test/DomainServiceTest/DomainServiceTest.csproj" -c Release --filter $filter

exit $LASTEXITCODE
