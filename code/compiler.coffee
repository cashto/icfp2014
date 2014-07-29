fs = require 'fs'
os = require 'os'

pad = (s, n) ->
    while s.length < n
        s = s + ' '
    return s
    
tokenize = (s) ->
    semiIdx = s.indexOf(';')
    s = s.substring(0, semiIdx) if semiIdx >= 0
    return s.split(' ').filter((i) -> i.length isnt 0)
    
filename = process.argv[2]
lines = fs
    .readFileSync(filename, { encoding: 'utf8' })
    .split(os.EOL)
    .map((i) -> tokenize(i))
    .filter((i) -> i.length isnt 0)

# Pass 1 -- generate labels.
labels = {}
funcArities = {}
localArities = {}
currentFunc = ''
pc = 0
for tokens in lines
    switch tokens[0]
        when '!func'
            currentFunc = tokens[1]
            labels[currentFunc] = pc
            funcArities[currentFunc] = tokens.length - 2
            for token,idx in tokens[2..]
                labels["#{currentFunc}$#{token}"] = idx
        when '!locals'
            localArities[currentFunc] = tokens.length - 1
            for token,idx in tokens[1..]
                labels["#{currentFunc}$#{token}"] = idx
            pc += 3
        when '!call'
            pc += 2
        else
            if tokens[0][tokens[0].length - 1] is ':'
                labels["#{currentFunc}$#{tokens[0].substring(0, tokens[0].length - 1)}"] = pc
            else
                pc += 1

# Pass 2 -- emit code.
currentFunc = ''
pc = 0
for tokens in lines
    switch tokens[0]
        when '!func'
            currentFunc = tokens[1]
            #console.log "BRK"
            #console.log "; !func #{currentFunc}"
        when '!locals'
            console.log "LDF #{pc + 3}"
            console.log "AP #{localArities[currentFunc]}"
            console.log "RTN"
            pc += 3
        when '!call'
            console.log "LDF #{labels[tokens[1]]}"
            inst = "AP #{funcArities[tokens[1]]}"
            #console.log "#{pad(inst, 40)} ; !call #{tokens[1]}"
            console.log inst
            pc += 2
        else
            if tokens[0][tokens[0].length - 1] isnt ':'
                inst = tokens
                    .map((i) -> 
                        if (i[0] is '[' and i[i.length - 1] is ']')
                            sym = i.substring(1, i.length - 1)
                            if labels["#{currentFunc}$#{sym}"]?
                                labels["#{currentFunc}$#{sym}"]
                            else if labels[sym]?
                                labels[sym]
                            else
                                console.log "ERROR: undefined find symbol '#{currentFunc}$#{sym}'"
                                process.exit()
                        else 
                            i)
                    .map((i) -> i.toString().toUpperCase())
                    .join(' ')
                console.log inst
                #console.log "#{pad(inst, 40)} ; #{pc}"
                pc += 1

                