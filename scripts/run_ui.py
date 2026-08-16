import subprocess

def main(debug: bool = False):
    # Build HomeBase Solution
    print("Building HomeBase Solution...")
    subprocess.run(["dotnet", "build", "HomeBase.slnx"], check=True)

    # Run HomeBase UI
    print("Running HomeBase UI...")
    cmd = [
        "dotnet", 
        "run", 
        "--project", 
        "HomeBase/HomeBase.csproj",
    ]
    if debug:
        print("Debug mode enabled. Waiting for debugger to attach...")
        cmd.extend(["--", "--wait-for-debugger"])

    try:
        subprocess.run(cmd, check=True)
    except KeyboardInterrupt:
        print("\nHomeBase UI stopped by user.")
    except subprocess.CalledProcessError as e:
        print(f"\nError running HomeBase UI: {e}")

if __name__ == "__main__":
    # Get args, look for --debug
    import sys
    if "--debug" in sys.argv:
        main(True)
    else:
        main(False)

