echo "How many games to launch [default: 2]?"
read games_count
games_count=${games_count:-2}

for (( i=0; i < $games_count; ++i ))
do
    export NUMBER_OF_PLAYERS=$GAMES_COUNT
    ${GODOT_EXECUTABLE} --path . &
done
