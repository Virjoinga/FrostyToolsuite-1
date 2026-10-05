# MathEntityExtender
This is a plugin for [Frosty Editor](https://github.com/CadeEvs/FrostyToolsuite/tree/1.0.6.3) designed to make editing and understanding Math Entities across all games easier.

## How it works
With this plugin, Math Entities have 3 types of values: Variables, Property Inputs, and Instructions. Variables allow you to, within the MathEntity, get and set different types of values. Property Inputs create new Inputs you can add in the Property Connections, and Instructions are all of the math you can apply to the Variables and Inputs.

![MathEntity](https://github.com/Ywingpilot2/FrostyMathEntityExtender/assets/136618828/992d317c-905e-4e41-9975-d8c1813dc457)

### Variables & Inputs
Variables and Inputs are able to store different types of data. Variables are able to store Integers, Floats, and Booleans. Inputs are able to store Integers, Floats, Booleans, Vectors and Transforms. 

Inputs get their values from the property connection of the same name, but can also be used by Instructions to store values. 

![InputEntity](https://github.com/Ywingpilot2/FrostyMathEntityExtender/assets/136618828/2aac7194-c26d-471f-9e09-6767ac6bb265)

Variables start as static with default values, their values unlike Inputs are not affected by anything outside of the MathEntity. Like Inputs, Variables can also have their values modified by the Instructions.

![VariableEntity](https://github.com/Ywingpilot2/FrostyMathEntityExtender/assets/136618828/7092a396-a70b-423a-ae8f-d6362c26668c)
### Instructions
Instructions are the heart of a MathEntity, as they contain the math needed to be done on the Variables and Inputs. Math Entities can do most things that Event and Property connections can do, except much faster and much cleaner.

![MathInstructionEntity](https://github.com/Ywingpilot2/FrostyMathEntityExtender/assets/136618828/c23512de-63a1-43c1-ade5-639aff9bd90c)

The top level of Instructions have 4 properties: Operation, Operation Type, Output To and Inputs;
Operation is what math we are doing, so for example if we wanted to add 2 integers together, we would set the Operation to Add: Int Int. The names past the semicolon describe the inputs the Math requires. The last one will always be the output value.

Operation Type describes the type of action we will be doing for this instruction, so for example, the Split Vec2 operation can be Operation type Split X, or Split Y. These specify what the inputs and outputs are per type. Types aren't used for most operations and can be set to none, and are most extensively used for Functions, which have many types for doing complex actions such as If(return a different value dependin on a boolean), Clamp(prevent an Int or Float from going above or below specified values), sqrt(get the Square Root of an Int or Float), and many more.

Output To is the Variable or Input this will save its value to. So for example, if we add variables A(which is 1) and B(which is 2), then set the Output to be A, A will then become 3 and all Instructions after this will treat A as 3.

Inputs are all of the different inputs this Operation/Operation Type requires, so for example, if we wanted to add 2 floats together, we would have 2 inputs for Add: Float Float

## What is and isn't currently supported
For personal reasons, this is still a WIP as I don't currently have the time to finish it completely. I should hopefully be able to add support for everything relating to Math Entities, though they come with so much that this will take time.

What is supported:
  - Loading/Decompiling Variables and Inputs of all types
  - Loading/Decompiling Instructions of all types
  - Loading/Decompiling most(if not all) Functions(aka Operation Types)
  - Saving/Compiling Variables of all types
  - Saving/Compiling Inputs of all types
  - Saving/Compiling Instructions with inputs of all types
  - Saving/Compiling Instructions with Integers, Floats, and Booleans

What isn't supported:
  - Loading/Decompiling Instructions which declare a new register/variable(this has a work around, if the loader/decompiler notices this it will create a new variable then reference that, though this means the decompiled result will not be accurate to what it actually is)
  - Loading/Decompiling Instructions with new MathOpCodes and Functions(the loader/decompiler will currently just fake its way through new ones, though this almost always results in incorrect inputs and outputs)
  - Saving/Compiling Instructions which output vectors and transforms(currently you can create new inputs with those types and have them be blank, and the compiler will just output to those, which should work fine, though this is cumbersome. Ideally, I would like to add support for Variables having vectors and transform types though this would be complex to compile)

## Additional notes/notices
This as I said previously is still WIP, so there are obviously bugs to work out. Here is a list of things to keep in mind as you are working with this plugin:
  - The compiler will sometimes error out and save instruction's references improperly, it should(hopefully) log an error each time this happens so you the user can try to debug it, and should hopefully happen rarely, though its hard to squash every time this happens as there is a lot of instances where 1 small thing in 1 specific scenario can go wrong.

  - I do not know of a way to update the property grid UI through this plugin, which means if you say, open an Input, set it to whatever you want it to reference, add a new variable and go back to that drop down, it will not update with the new Variable. I am sure there is a way to fix this, and if you know how please let me know! You can find me on discord at ywingpilot2. It would be very useful to know something like this as it has plagued this project a bit

  - As always with something like this, crashes are possible. As far as I am aware I have minimized them to the degree of no longer existing, though as I said before there will always be the one random case where something breaks or whatever. So if you do encounter any crashes, please report them to me.
