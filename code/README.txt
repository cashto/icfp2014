Note that this is a provisional README.  Full description will be posted later 
at

    https://dl.dropboxusercontent.com/u/31272201/icfp/2014/index.html

Team cashto is, as it has been since 2007, the work of one Chris Ashton from 
Seattle.

File contents:

    *.cs: source code for simulator (not 100% identical to reference 
      implementation, but close enough)
    *.mac: macro assembly code for lambdaman and ghost (yes, they were
      written by hand)
    *.coffee: "compilers" for the macro assembly code (mostly they just 
      linking symbols)

LambdaMan is your basic-depth first search -- to a depth of 5 moves (3 for 
maps with more than 8 ghosts, since the number of iters taken is roughly
proportional to the number of ghosts on the map). LambdaMan loves eating pills,
fruits, and ghosts, and hates dying, backtracking, and getting too far away 
from the "objective" (which is the fruit if it is up, and the closest dot 
otherwise).

LambdaMan also doesn't like to eat powerups, unless he can get a ghost with
it too.

Ghost is more-or-less similar to the original game (red chases lambdaman 
directly, pink chases some distance ahead, cyan tries to cut off the escape 
route from red).  Orange is a little different - it tries to orbit lambdaman
(e.g., moving perpendicular to the line between himself and lambdaman). To 
spread things out at the beginning, each ghost has a "scatter" mode 25% 
of the time (moving to each corner).