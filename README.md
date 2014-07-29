# ICFP 2014

Team cashto is, as it has been since 2007, the work of one Chris Ashton from Seattle.

* [Discuss on Reddit](http://www.reddit.com/r/icfpcontest/comments/2c0l3f/cashtos_2014_icfp_contest_writeup/)!
* [Time lapse video](http://youtu.be/-QlvQVDuO0c)!
* [Final whiteboard](https://dl.dropboxusercontent.com/u/31272201/icfp/2014/whiteboard.jpg)

### Source Code Statistics

* Simulator: 1144 lines of C# code.
* Visualizer: 188 lines of HTML / Javascript.
* LambdaMan source code: 928 lines of custom macro assembly language.
* LambdaMan compiler: 104 lines of CoffeeScript.
* Ghost source code: 189 lines of custom macro assembly language.
* Ghost compiler: 45 lines of CoffeeScript.

### Solution AI Statistics

* LambdaMan: 830 instructions.
* Ghost: 119 instructions.

### Day by Day

* Day 1: simulator, visualizer, and simple LambdaMan compiler and greedy AI.
* Day 2: Improved LambdaMan AI.
* Day 3: Ghost AI, refinements of Lambdaman AI.

### Overview

This year's task was to write an AI that plays Pacman clone call "LambdaMan". The AI program is written in a stack-based assembly language documented in the task description. You must also write a program in a *different* assembly language for the ghost AIs. Your LambdaMan AI competes against other teams' ghost AIs, and vice versa.

My LambdaMan AI is your basic depth-first search -- to a depth of 5 moves (3 for maps with more than 8 ghosts, since the number of iters taken is roughly proportional to the number of ghosts on the map). LambdaMan loves eating pills, fruits, and ghosts, and hates dying, backtracking, and getting too far away from the "objective" (which is the fruit if it is up, and the closest dot otherwise).

LambdaMan also incurrs a small **penalty** for eating powerups, and will avoid eating them unless it leads to (much larger) bonus for eating ghosts afterwards.

Ghost is more-or-less similar to the original game (red chases lambdaman directly, pink chases some distance ahead, cyan tries to cut off the escape route from red).  Orange is a little different: the intention was to orbit LambdaMan, moving perpendicular to the line between himself and LambdaMan -- but due to a small bug, it's mostly a direct-chaser like red.

To spread things out at the beginning, each ghost has a "scatter" mode 25% of the time (moving to each corner).

### Remarks

The lightning round AI was pretty bare-bones (see a dot?  eat a dot.  don't see a dot?  move randomly).  This was owing to the fact that I didn't start working on it until about midnight, five hours before the deadline. Most of the first day was spent working on the simulator. In retrospect, I could probably could have skipped that altogether and just used the online implementation, but on the other hand, I had a fairly disasterous showing in 2011 in large part due to my reliance on the organizers' reference implementation, and as a result I had very little insight into how the VM worked, and not enough tools for debugging my AI's issues, besides the very terse error messages I would get back.

I thought about writing a Lisp-like DSL -- I thought it might take too long, and writing AI assembly language by hand is fairly straightforward; but in the process I wrote dozens of little bugs where I would pop too many things off the stack, or not pass enough parameters to a function, and debugging each one one was tedious and took some time.  Using a DSL would have eliminated that.  I probably made the wrong tradeoff here.

I never used the DUM / RAP / TRAP opcodes.  I'm still a little hazy on why I would ever want to use RAP rather than AP.  I did use TAP quite a bit -- I think the difference is that TAP still keeps building environment frames, and if the depth of my recursion ever got big, that might be a problem, but since infinite recursion was not needed in this contest, I don't think it was necessary. 

I also never understood why GHC assembly has a JLT opcode (which is unnecessary; just use JGT with the operands reversed).  But no JNE?  Yeah, thanks a lot, guys.

Midway through the 2nd day I discovered an ambiguity in the spec that I reported to the organizers -- rounding behavior of DIV was not defined.  In C#, division rounds to zero; in their reference implementation, it rounds to negative infinity.  Fortunately, the latter behavior makes it MUCH easier to write a MOD function that deals with negative arguments correctly ...

I wrote a lot of code to try to prevent LambdaMan from eating powerpills unless the average distance to the ghosts was beneath some cutoff.  In the end I tossed it: the heuristic of just unconditionally penalizing powerpills worked much better.

With a standard map and four ghosts, each iteration of the LambdaMan step function takes between 100,000 and 1,000,000 cycles.  There was a upper bound of ~3,000,000 cycles per iteration, so I felt a search depth of 5 was relatively safe. 

Late on the 3rd day I did some basic profiling and discovered about 80% of my execution time was in the function get_map (which takes the encoding of the map, an x coordinate, a y coordinate, and returns the contents of that map cell). This was not unexpected, since this called once for every ghost at every node in the search tree, and it's a linear walk through a map structure.  In the back of my mind I had the thought of trying to pre-process the map structure at the start of every iteration to make this more efficient, O(log) time or even constant-time for adjacent cells, but I didn't have enough time to work on it.

One thing I **did** do was to elide the get_map check for faraway ghosts, figuring in the worst case they could walk through walls to get to lambdaman.

Since get_map's time was expected to increase with the number of ghosts and the size of the map, I reduce the search depth to 3 for maps with more than 8 ghosts.  My AI is still pretty vulnerable to running out of time on big maps with 8 or fewer ghosts, but I'm hoping the organizers don't test that.

### Interesting AI Bugs

At the beginning of each step, LambdaMan picks an "objective" square -- usually the closest dot.  If LambdaMan cannot score any immediate points, it at least it is rewarded for paths that wind up closer to the objective square than when it began. Paradoxically this would cause LambdaMan to sometimes oscillate or AVOID picking up dots, since it would reason that it could get a better score by initially moving away, then picking up the dot at the end of its search path. This was fixed by adding a penalty for backtracking, as well as a 10% per search ply decrease in rewards to encourage "greediness".

LambdaMan always assumes ghosts will head directly for him (in other words, he only considers one possible branch if ghosts have multiple move options). For example, imagine that LambdaMan is 10 squares to the left, and 5 squares below the ghost. The ghost **really** wants to move left, but it also kinda wants to move downwards too.  Initially, LambdaMan would see if the ghost could move left, and if it could not (because of a wall or a path reversal), it would pick the first available option out of (up, right, down, left).  When coding the ghost AI, I realized this was suboptimal -- the ghost might move upwards even when a downwards move would be better.  As a result, both my ghost AI and LambdaMan AI now consider an alternate direction when the primary direction is not available.

For a while, LambdaMan would never try to run down a frightened ghost, reckoning that the ghost could flee faster than he could chase them.  It's hard to accurately simulate slow movement since to LambdaMan, the quantum of time is a step, not a single tick -- so I decided to **inaccurately** simulate slow movement instead. LambdaMan believes that ghosts *stop completely* when they turned frightened.

LambdaMan would often die unnecessarily by running straight into ghosts -- since the search algorithm processes LambdaMan moves and ghost moves simultaneously, it would figure that sometimes LambdaMan would go to the ghost's location, and the ghost would go to LambdaMan's current location, and no collision would occur.  Initially this was fixed by considering adjacency as a collision, not just overlap -- but this is too conservative; the final implementation checks the new LambdaMan location for direct overlap with **either** the ghost's old location or the new one.

Another cause for unnecessary LambdaMan death was invisible ghosts -- LambdaMan would sometimes chase invisible ghosts, which would inevitably end in tragedy the moment once fright mode expires. This was fixed by treating invisible ghosts as dangerous "live" ghosts, rather than tasty "frightened" ones.

In the last ten minutes of the contest, sleep-deprived me was desperately scrambling to fix a bug where invisible ghosts were inappropriately considered "stopped", like frightened ones, and consequently LambdaMan was doing a poor job avoiding them.
