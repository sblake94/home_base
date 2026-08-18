# This script builds the HomeBase Solution and runs each test project in a separate process. It is intended to be run from the root of the repository.

import subprocess

def main():
    # Build HomeBase Solution
    print("Building HomeBase Solution...")
    subprocess.run(["dotnet", "build", "HomeBase.slnx"], check=True)

    test_projects = [
        ("HomeBase Core Tests", "HomeBase.Core.Tests/HomeBase.Core.Tests.csproj"),
        ("HomeBase UI Tests", "HomeBase.Tests/HomeBase.Tests.csproj"),
    ]

    for test_name, test_project in test_projects:
        print(f"Running {test_name}...")
        subprocess.run(["dotnet", "test", test_project], check=True)

if __name__ == "__main__":
    main()