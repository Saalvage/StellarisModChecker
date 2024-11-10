# Stellaris Mod Checker

A simple CLI utility application intended to find any mods that differ between two installed instances of Stellaris.

The Steam workshop exhibits weird behavior when it comes to mod downlodads sometimes and this may result in clients being unable to play together due to opaque differences in the installed mods, even if the selected playsets are identical.
One reason for this may be updates to mods that get acquired by one client but not by another but different causes also seem to exist.
There is no built-in way to assert mod consistency, which this tool is intended to alleviate.

## Usage

Usage should be pretty simple and intuitive, here's a tutorial on how to check if two users have any inconsistent mods:

1. User A generates a hash file for their installed mods (automatically done on first-time startup)
2. User A sends the generated file to user B
3. User B uses the application to automatically compare user A's hashes with their own
4. The application reports any mods that are incompatible by their workshop ID!
5. Disable the selected mods, force a redownload on both sides by deleting the mods' folders or have one user send the other their mod files

When mods are added/updated the hashes need to be regenerated, which also needs to be done for the user comparing a hash file sent to them such as user B in the example above.

## Feedback

The application has personally managed to resolve mod incompatibility issues successfully multiple times.
If you have any feedback/questions/problems feel free to open an issue.
