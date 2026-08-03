# This script simply builds the HomeBase Solution and runs the HomeBase Core Tests. It is intended to be run from the root of the repository.

import subprocess

def main():
    # Build HomeBase Solution
    print("Building HomeBase Solution...")
    subprocess.run(["dotnet", "build", "HomeBase.slnx"], check=True)

    # Run HomeBase Core Tests
    print("Running HomeBase Core Tests...")
    subprocess.run(["dotnet", "test", "HomeBase.Core.Tests/HomeBase.Core.Tests.csproj"], check=True)

if __name__ == "__main__":
    main()