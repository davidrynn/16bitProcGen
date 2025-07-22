# Unity DOTS System Authoring Checklist

## ✅ Always Mark DOTS Systems as `partial`
- [ ] Every class inheriting from `SystemBase`, `ISystem`, or similar DOTS base types is marked as `partial`.
  ```csharp
  public partial class MySystem : SystemBase
  {
      // ...
  }
  ```

## ✅ One Class Per File
- [ ] Each DOTS system class is defined in its own `.cs` file, with the filename matching the class name.

## ✅ No Duplicate Class Names
- [ ] No other files in the project define a class with the same name and namespace as your system.

## ✅ Clean Up After Refactors
- [ ] After renaming, moving, or deleting system files, clean the `Library`, `Temp`, and `obj` folders and restart Unity to remove stale generated code.

## ✅ Avoid Defining Systems in Markdown or Non-Code Files
- [ ] Do not include full system class definitions in markdown or documentation files, unless they are fully commented out or inside code blocks that cannot be parsed by Unity.

## ✅ Use Namespaces Consistently
- [ ] Use consistent namespaces for your systems to avoid accidental collisions.

## ✅ Check for Source Generator Output
- [ ] If you get a "namespace ... already contains a definition for ..." error, check the `Temp/GeneratedCode/Assembly-CSharp/` folder for generated files with the same class name.

---

## Quick Fix for Duplicate System Errors

1. Ensure your system class is marked as `partial`.
2. Clean the `Library`, `Temp`, and `obj` folders.
3. Restart Unity and let it reimport the project.

---

**Tip:**  
Add this checklist to your project’s `README.md` or developer documentation to help your team avoid common DOTS system pitfalls! 