![UnitySharpNEAT_logo](https://user-images.githubusercontent.com/1511848/98319619-72429180-1fe1-11eb-9c4f-89d36f19cfc9.jpg)


# UnitySharpNEAT
NEAT is NeuroEvolution of Augmenting Topologies; an evolutionary algorithm devised by Kenneth O. Stanley.
[SharpNEAT](https://github.com/colgreen/sharpneat) is a complete implementation of NEAT written in C# and targeting .NET (on both MS Windows and Mono/Linux). 

This project is a continuation and refactor of the [UnityNEAT](https://github.com/lordjesus/UnityNEAT) project, which implements [SharpNEAT](https://github.com/colgreen/sharpneat) into Unity. 

## Refactoring Changes

 - Abstraction
 - Added object pooling
 - Reduced and streamlined the use of Coroutines greatly
 - Added support for multiple experiments
 - Bug fixes
 - Code cleanup, performance optimization
 - Folder structure reorganization
 - Method, variable and class renamings to fit code conventions and to be more descriptive
 - Commented code

## Usage

To use NEAT in your Unity project, simply download the ```UnitySharpNEAT``` folder and import it to your Unity project. Alternatively, you can just clone this repository to get a working UnitySharpNEAT project out of the box. In the ```UnitySharpNEAT``` folder you will find an example experiment, the [CarExperiment](https://github.com/flo-wolf/UnityNEAT#example-car-experiment), which you can use for inspiration.

You will need to follow these steps to set up your own experiment:

1. In your scene, add an empty GameObject and attach the ```NeatSupervisor``` MonoBehaviour to it. You can hover over each parameter in the Inspector to get an explaining tooltip. This script acts as the bridge between Unity and SharpNEAT. It is responsible for managing the lifecycle of the evolution, by starting/stopping it and managing the Units being evolved. Your Unit is the thing which you are evaluating; a car, a robot, a colourful 3D shape.

2. In the ```NeatSupervisor``` Inspector you can set the number of inputs and outputs by changing  ```NetworkInputCount``` and ```NetworkOutputCount```. These need to be adjusted to fit your use case, so that the amount of inputs and outputs fits your Unit. 

3. Create a Prefab that will be your Unit, and create and attach a script to your Unit Prefab that inherits from ```UnitController```. The ```NeatSupervisor``` will instantiate and manage a population of these Units. Classes deriving from ```UnitController``` only need to implement the following four methods - everything else is handled by the base class.
```c#
public class ExampleController : UnitController
{
    protected override void UpdateBlackBoxInputs(ISignalArray inputSignalArray)
    {
        // Called by the base class on FixedUpdate

        // Feed inputs into the Neural Net (IBlackBox) by modifying its InputSignalArray
        // The size of the input array corresponds to NeatSupervisor.NetworkInputCount

        inputSignalArray[0] = someSensorValue;
        inputSignalArray[1] = someOtherSensorValue;
        //...
    }

    protected override void UseBlackBoxOutpts(ISignalArray outputSignalArray)
    {
        // Called by the base class after the inputs have been processed

        // Read the outputs and do something with them
        // The size of the array corresponds to NeatSupervisor.NetworkOutputCount

        someMoveDirection = outputSignalArray[0];
        someMoveSpeed = outputSignalArray[1];
        //...
    }

    public override float GetFitness()
    {
        // Called during the evaluation phase (at the end of each trail)

        // The performance of this unit, i.e. it's fitness, is retrieved by this function.
        // Implement a meaningful fitness function here

        return 0;
    }

    protected override void HandleIsActiveChanged(bool newIsActive)
    {
        // Called whenever the value of IsActive has changed

        // Since NeatSupervisor.cs is making use of Object Pooling, this Unit will never get destroyed. 
        // Make sure that when IsActive gets set to false, the variables and the Transform of this Unit are reset!
        // Consider to also disable MeshRenderers until IsActive turns true again.
    }
}
```
4. In the ```NeatSupervisor``` GameObject drag your Unit Prefab to the ```Unit Controller Prefab``` slot. 

5. In the subfolder ```Resources``` there is a file called ```CarExperiment.config.xml``` in which you can specify the parameters for the evolution, such as ComplexityRegulationStrategy and PopulationSize. See the original [SharpNEAT] project for more settings. Most importantly, the config file lets you set the Name of the experiment, which will be used for identifying savefiles. If you want to use multiple config files for different experiments, feel free to duplicate and rename them. In the ```NeatSupervisor``` you can set the config filename to be loaded. 

6. To get a simple UI that allows you to start/stop the evolution, track its progress and adjust the timescale, drag the ```NeatUI``` Prefab from the ```UI``` folder into your scene. Make sure that the ```NeatSupervisor``` gameobject is referenced. When in Playmode, you can now hit the ```Start``` button to start the evolution process.



## Example: Car experiment

In the ```CarExperiment``` folder you will find the ```CarExperimentScene```, in which cars can learn to race around a race track. 
The ```CarController``` script controls a single car. Five distance sensors measure its distance to walls, which get input into its Neural Network.  The Output corresponds to steering and thrust control. Its fitness is calculated by the amount of road piceces it passed, how many laps it drove and how few wall hits it has taken.

A video of the evolution using the old UnityNEAT project can be seen on [youtube]. The UnitySharpNEAT experiment looks a bit different, but generally this will give you an idea of what is being achieved.


## Project Future
Currently the same SharpNEAT version which was employed by UnityNEAT is still being used, albeit SharpNEAT has received lots of updates over the years. The biggest refactor of the SharpNEAT project, [sharpneat-refactor](https://github.com/colgreen/sharpneat-refactor), is currently in the works. In the near future the SharpNEAT part of this project will be replaced by the refactored version.
There are also plans to allow users to use HyperNEAT instead of just NEAT. This will require some careful planning though, since HyperNEAT needs a Substrate which configuration can differ vastly from project to project. Providing an intuitive way to design these substrates without having to get deep into the code is a challenge to be solved first.


## Behind the Scenes
If you are still new to NEAT, I would reccomend [this article](https://towardsdatascience.com/neat-an-awesome-approach-to-neuroevolution-3eca5cc7930f) as a quick introduction. There are also lots of different papers online and good explaination videos on youtube. It's also worth to take a look at the links and notes on the [SharpNEAT Website](https://sharpneat.sourceforge.io/). 
To get a deeper understanding of what is actually going on behind the scenes, feel free to dig around the code and have a look at all of the base classes. I tried to add as many comments as possible. If there are still any questions left, just open an Issue.

[UnityNEAT]:(https://github.com/lordjesus/UnityNEAT)
[SharpNEAT]:http://sharpneat.sourceforge.net/
[Center for Computer Games Research]:http://game.itu.dk/index.php/About
[master's thesis]:http://jallov.com/thesis
[@DanielJallov]:https://twitter.com/DanielJallov
[youtube]:http://youtu.be/sHc9u67JPWc
[MIT License]:http://opensource.org/licenses/MIT
