# Parsec.Dotnet task runner. Run `just` to see every recipe.
#
# Recipes assume a POSIX shell. On Windows use Git Bash, which the git
# hooks in .husky already require.

set shell := ["bash", "-uc"]
set dotenv-load := true

# List the available recipes.
default:
    @just --list --unsorted

# Restore local dotnet tools and NuGet packages.
restore:
    dotnet tool restore
    dotnet restore

# Build in Release. Warnings are errors.
build:
    dotnet build --configuration Release

# Run the test suite on every target framework.
test:
    dotnet test --configuration Release

# Run the tests and write VS coverage XML that SonarCloud can import.
coverage:
    dotnet test --configuration Release --results-directory TestResults \
        --coverage --coverage-output-format xml

# Report formatting violations without changing files.
format-check:
    dotnet format --verify-no-changes

# Apply formatting fixes in place.
format:
    dotnet format

# Run the same checks as the pre-push git hook.
verify: build test format-check

# Build both documentation locales into artifacts/docs.
docs:
    dotnet docfx docs/docfx.json --warningsAsErrors
    dotnet docfx docs/docfx.zh-tw.json --warningsAsErrors

# Serving the combined output folder is what makes the language switcher
# and the /api/ links resolve, because both are root-relative.
# Build both locales and serve them on http://localhost:8080.
docs-serve: docs
    dotnet docfx serve artifacts/docs --port 8080 --open-browser

# Analyse with SonarCloud. Reads SONAR_TOKEN from .env.
sonar:
    dotnet dotnet-sonarscanner begin \
        /k:"marvin-hsu_Parsec.Dotnet" \
        /o:"marvin-hsu" \
        /d:sonar.host.url="https://sonarcloud.io" \
        /d:sonar.token="$SONAR_TOKEN" \
        /d:sonar.cs.vscoveragexml.reportsPaths="TestResults/**/*.xml" \
        /d:sonar.coverage.exclusions="tests/**"
    dotnet build --configuration Release
    dotnet test --no-build --configuration Release --results-directory TestResults \
        --coverage --coverage-output-format xml
    dotnet dotnet-sonarscanner end /d:sonar.token="$SONAR_TOKEN"

# BROKEN: Stryker needs VSTest but this repo runs Microsoft.Testing.Platform.
# Upstream issue: https://github.com/stryker-mutator/stryker-net/issues/3094
# Mutation testing. Does not work yet, see the note above.
mutate:
    cd tests/Parsec.Client.Tests && dotnet dotnet-stryker

# Delete build output, test results and the generated API reference.
clean:
    dotnet clean --configuration Release
    rm -rf artifacts TestResults
    rm -f docs/api/*.yml docs/api/.manifest
