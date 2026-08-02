# This script simply builds the HomeBase Solution and runs the HomeBase Core Tests. It is intended to be run from the root of the repository.

def main():
    # Build HomeBase Solution
    print("Building HomeBase Solution...")
    import subprocess
    subprocess.run(["dotnet", "build"], check=True)

    # Run HomeBase Core Tests
    print("Running HomeBase Core Tests...")
    subprocess.run(["dotnet", "test"], check=True)

if __name__ == "__main__":
    main()