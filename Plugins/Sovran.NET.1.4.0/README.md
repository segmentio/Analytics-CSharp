// add badges and stuff here

# Sovran.NET
Small, Efficient, Easy. State Management for .Net.

Sovran's goal is to be minimal, efficient, easy to implement and to make debugging state changes effortless.

While it is a rather opinionated library, we hope that you'll find said opinions to be with good reason.  While it
is somewhat similar to things like Redux and Flux there are some natural differences when applied to .Net.

### We don't like large state structures

Large state structures just aren't terribly useful.  Subscribers typically only care about a small subset of the
data contained within.  We have opted to allow multiple state structures to be supplied and work in unison.
This has the benefit of subscribers to be given *just* the parts of the state that they are interested in.  It's highly
recommended that state structures only contain properties that relate to one another in some obvious way.

By using data classes to define state objects, we benefit from the natural copy mechanism within .Net to make sure
no direct access to state is given to subscribers.

### We don't like artificial constraints

We've been very careful to not dictate how you write your code.  If you want one state structure to subscribe to
another unrelated state, we assume you have a good reason.   If you want a single giant state structure, while it's
not our first choice of solutions, you can do that if you need to.  If you need to be unconventional, do it.  Everything
has a time and place.

### Types should be explicit, but inferred where possible

You'll notice that when using Sovran, it's actually the subscription input that defines the type of state that a given
closure is intended to work against.  This allows us to avoid having developers supply the type twice.

Example:
```.Net
store.subscribe(subscriber = this, stateClazz = MyState::class) { state ->
    // MyState was updated, react to it in some way.
    print(state)
}
```
In the example above, `MyState` is defining the generic type needed by the subscribe call.

### We want it to be *very* debuggable

It is very common for bugs to occur in code.  This library is architected such that most anywhere within the
library as well as your own code, the stack trace shows the exact point a state change was initiated and all
points in between.

## Getting Started

// TODO

## Contributing


- Please see our [code of conduct](CODE_OF_CONDUCT.md)
- To submit a bug report or feature request, [file an issue here](issues).
- To develop on `Sovran` or propose changes, see [our contributors documentation](.github/CONTRIBUTING.md).

## License
```
MIT License

Copyright (c) 2021 Segment

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```
