import subprocess

def main():
    # Build HomeBase Solution
    print("Building HomeBase Solution...")
    subprocess.run(["dotnet", "build", "HomeBase.slnx"], check=True)

    # Run HomeBase UI
    print("Running HomeBase UI...")
    subprocess.run(["dotnet", "run", "--project", "HomeBase/HomeBase.csproj"], check=True)

if __name__ == "__main__":
    main()

