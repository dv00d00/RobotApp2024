# Development Process

## Prototyping with F#:

I began with an F# prototype to validate my modeling approach in a functional programming paradigm. This allowed me to refine the core logic and data structures before transitioning to the final implementation.

## C# Implementation with LanguageExt:

Leveraging the functional programming capabilities provided by the LanguageExt library, I translated the prototype into a C# application. This transition demonstrated my ability to adapt functional principles within an object-oriented language.

## Iterative Testing and Debugging:

Once the application produced expected results for the provided sample inputs, I extended its functionality by implementing a visualization feature. This feature displayed the robot's state at each execution step, greatly enhancing my ability to debug and address edge cases effectively.

## Randomized Testing with FsCheck:

To ensure robustness, I integrated randomized property-based tests using FsCheck. This approach allowed me to test the application against a wide range of scenarios, uncovering edge cases that traditional test cases might miss.

## Performance Analysis and Optimization:

Executing 1k randomized tests took longer than expecting and I've decided to benchmark app a bit. Implemented "optimzed" imperative parser using spans to compare against. Abstractions are not free. 

## Another look at the code

There are several questionable design decisions in the codebase that I would love to discuss:

Having `Validated` models which are impossible to create in invalid state sounds like a great choice, but the problem domain is small enough to make it unnecessary. 

It kind of feels like an implementation detail leak, could probably introduce a union which would combine `RuntimeError` and `RobotState` into a something like `ComputationResult` 
```public static Lst<Either<RuntimeError, RobotState>> TravelAll(ValidatedFile file, IRuntimeLog? log = null)``` 

It was absolutely unnecessary to implement robot logging, I did it partly to simulate a business requirements change. I've initially tried using `Reader`, signature has become huge.

Similar story happened in the console host, it kind of felt far-fetched to use `IO` or `Try`. I am genuinely curious how would you approach this.

## Results and Insights

It was a great challenge, I've really missed writing functional code. I might have become "too" excited which ended up in the extra features no one asked for. Testing different approaches was really fun though.

I believe the right way to treat this code as a sort pull request, rather than a final solution. I would love to hear your thoughts on the codebase and the design decisions I've made.

