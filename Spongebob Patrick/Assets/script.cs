using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;

public class script : MonoBehaviour {
    public KMBombInfo bomb;
    public KMAudio bombAudio;
    public KMBombModule module;

    static int ModuleIdCounter = 1;
    int ModuleId;
    private bool moduleSolved = false;

    //ints in order:
    //0=spongebob 1=patrick 2=squidward 3=sandy 4=larry 5=pearl 6=mr krabs
    //7=plankton 8=karen 9=gary 10=mrs puff 11=bunchbob 12=king neptune
    public Material[] characterMaterials;
    string[] characterUneditedNames = new string[13] { "Spongebob", "Patrick", "Squidward", "Sandy", "Larry", "Pearl", "Mr. Krabs", "Plankton", "Karen", "Gary", "Mrs. Puff", "Bunchbob", "King Neptune" };

    public KMSelectable[] buttons;
    public Renderer[] buttonLabels;
    int[] chosenCharacters = new int[4];

    int[,] characterTable = new int[7, 7]
    {
        { 0,0,1,9,2,1,3 },
        { 7,6,8,11,10,5,8 },
        { 4,12,5,2,6,7,7 },
        { 1,7,6,3,0,10,11 },
        { 8,9,2,7,9,2,10 },
        { 4,6,2,4,5,6,2 },
        { 7,11,11,12,6,3,12 }
    };
    int[] pooledCharacters = new int[10];

    string[,] maze = new string[4, 4]
    {
        { "","","","" },
        { "","","","" },
        { "","","","" },
        { "","","","" }
    };
    bool[,] travelledCells = new bool[4, 4]
    {
        { false, false, false, false },
        { false, false, false, false },
        { false, false, false, false },
        { false, false, false, false }
    };
    int quota;
    int amountTravelled = 1;
    Vector2Int initialPlayerPosition;
    Vector2Int playerPosition;
    List<Vector2Int> allGoalPositions = new List<Vector2Int>();
    List<Vector2Int> goalPositions = new List<Vector2Int>();

    int[] directionIndices = new int[4] { -1, -1, -1, -1 };
    string[] directionNames = new string[4] { "Up", "Right", "Down", "Left" };

    // Use this for initialization
    void Start() {
        //button generation
        string logCharacters = "";
        List<int> availableIndexes = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
        for (int i = 0; i < 4; i++)
        {
            chosenCharacters[i] = availableIndexes[Random.Range(0, availableIndexes.Count)];
            pooledCharacters[i] = chosenCharacters[i];
            availableIndexes.Remove(chosenCharacters[i]);
            buttonLabels[i].material = characterMaterials[chosenCharacters[i]];
            logCharacters += characterUneditedNames[chosenCharacters[i]];
            if (i < 3)
                logCharacters += ", ";
        }
        DebugMsg("The buttons, in reading order, are 🎶 " + logCharacters + " 🎶");

        //finding characters / matching pairs
        int currentPooledCharacterIndex = 4;
        logCharacters += ", ";
        for (int i = 0; i < 3; i++)
        {
            Vector2Int firstCharacterPosition = positionInBigTable(chosenCharacters[i], i);
            for (int j = i + 1; j < 4; j++)
            {
                Vector2Int secondCharacterPosition = positionInBigTable(chosenCharacters[j], j);
                Vector2Int pooledCharacterPosition = Midpoint(firstCharacterPosition, secondCharacterPosition);
                pooledCharacters[currentPooledCharacterIndex] = characterTable[pooledCharacterPosition.y, pooledCharacterPosition.x];

                logCharacters += characterUneditedNames[pooledCharacters[currentPooledCharacterIndex]];
                if (currentPooledCharacterIndex < 9)
                    logCharacters += ", ";

                currentPooledCharacterIndex++;
            }
        }
        DebugMsg("The pooled characters are 🎶 " + logCharacters + " 🎶");

        char[,] mazeArrows = new char[4, 4]
        {
        { '.', '.', '.', '.' },
        { '.', '.', '.', '.' },
        { '.', '.', '.', '.' },
        { '.', '.', '.', '.' } };
        int[,] mazeAreas = new int[4, 4]
        {
        { 0,0,0,0 },
        { 0,0,0,0 },
        { 0,0,0,0 },
        { 0,0,0,0 }
        };
        int[] timesCharacterEncountered = new int[13];
        for (int i = 0; i < 10; i++)
        {
            int fromCharacter = pooledCharacters[i];
            int toCharacter = pooledCharacters[(i + 1) % 10];
            Vector2Int fromCharacterPosition = positionInDiagram(fromCharacter, timesCharacterEncountered[fromCharacter]);
            timesCharacterEncountered[fromCharacter]++;
            Vector2Int toCharacterPosition = positionInDiagram(toCharacter, timesCharacterEncountered[toCharacter]);
            Vector2Int positionOffset = new Vector2Int(toCharacterPosition.x - fromCharacterPosition.x, toCharacterPosition.y - fromCharacterPosition.y);

            if (Mathf.Abs(positionOffset.x) == Mathf.Abs(positionOffset.y)) //slope = +-1/same position which has no effect on the maze
            {
                Debug.LogFormat("[Spongebob Patrick Squidward Sandy #{0}] Traveling from {1} to {2} made no change to the maze.", ModuleId, characterUneditedNames[fromCharacter], characterUneditedNames[toCharacter]);
                continue;
            }
            if (positionOffset.x == 0 || positionOffset.y == 0) //slope is horizontal or vertical
            {
                char arrow = positionOffset.x == 0 ? (positionOffset.y > 0 ? 'v' : '^') : (positionOffset.x > 0 ? '>' : '<');
                int numberOfPoints = Mathf.Max(Mathf.Abs(positionOffset.x), Mathf.Abs(positionOffset.y)); //how many points need to be changed

                for (int j = 0; j < numberOfPoints; j++)
                {
                    Vector2 modifyPoint = Vector2.Lerp(fromCharacterPosition, toCharacterPosition, (float)j / (float)numberOfPoints);
                    //print(lerpPoint.x + " : " + lerpPoint.y);
                    mazeArrows[Mathf.RoundToInt(modifyPoint.x), Mathf.RoundToInt(modifyPoint.y)] = arrow;
                }

                char lastPointArrow = mazeArrows[toCharacterPosition.x, toCharacterPosition.y];
                if (arrow == '^' && lastPointArrow == 'v' ||
                    arrow == '>' && lastPointArrow == '<' ||
                    arrow == 'v' && lastPointArrow == '^' ||
                    arrow == '<' && lastPointArrow == '>')
                    mazeArrows[toCharacterPosition.x, toCharacterPosition.y] = '.';
            }
            else if (Mathf.Abs(positionOffset.x) == 1 || Mathf.Abs(positionOffset.y) == 1) //slope is in form 1/n or n - can't be 1 (checked earlier)
            {
                bool xOffsetIsOne = Mathf.Abs(positionOffset.x) == 1;
                char arrow = xOffsetIsOne ? (positionOffset.x > 0 ? '>' : '<') : (positionOffset.y > 0 ? 'v' : '^');
                int numberOfPoints = 0;
                if (xOffsetIsOne)
                    numberOfPoints = Mathf.Abs(positionOffset.y);
                else
                    numberOfPoints = Mathf.Abs(positionOffset.x);

                for (int j = 1; j < numberOfPoints; j++)
                {
                    Vector2 modifyPoint = Vector2.Lerp(fromCharacterPosition, toCharacterPosition, (float)j / (float)numberOfPoints);
                    //print(modifyPoint.x + " : " + modifyPoint.y);
                    if (xOffsetIsOne)
                        mazeArrows[fromCharacterPosition.x, Mathf.RoundToInt(modifyPoint.y)] = arrow;
                    else
                        mazeArrows[Mathf.RoundToInt(modifyPoint.x), fromCharacterPosition.y] = arrow;
                }
            }
            else //slope is in form +-2/3 or +-3/2 - checked every other possibility
            {
                bool xOffsetIsTwo = Mathf.Abs(positionOffset.x) == 2;
                char arrow = xOffsetIsTwo ? (positionOffset.y > 0 ? 'v' : '^') : (positionOffset.x > 0 ? '>' : '<');

                Vector2 modifyPoint = Vector2.Lerp(fromCharacterPosition, toCharacterPosition, .5f);
                //print(modifyPoint.x + " : " + modifyPoint.y);
                if (xOffsetIsTwo)
                    mazeArrows[Mathf.RoundToInt(modifyPoint.x), positionOffset.y > 0 ? Mathf.FloorToInt(modifyPoint.y) : Mathf.CeilToInt(modifyPoint.y)] = arrow;
                else
                    mazeArrows[positionOffset.x > 0 ? Mathf.FloorToInt(modifyPoint.x) : Mathf.CeilToInt(modifyPoint.x), Mathf.RoundToInt(modifyPoint.y)] = arrow;

                arrow = xOffsetIsTwo ? (positionOffset.x > 0 ? '>' : '<') : (positionOffset.y > 0 ? 'v' : '^');
                for (int j = 1; j < 3; j++)
                {
                    modifyPoint = Vector2.Lerp(fromCharacterPosition, toCharacterPosition, j / 3f);
                    //print(modifyPoint.x + " : " + modifyPoint.y);
                    if (xOffsetIsTwo)
                        mazeArrows[positionOffset.x > 0 ? Mathf.FloorToInt(modifyPoint.x) : Mathf.CeilToInt(modifyPoint.x), Mathf.RoundToInt(modifyPoint.y)] = arrow;
                    else
                        mazeArrows[Mathf.RoundToInt(modifyPoint.x), positionOffset.y > 0 ? Mathf.FloorToInt(modifyPoint.y) : Mathf.CeilToInt(modifyPoint.y)] = arrow;
                }
            }

            Debug.LogFormat("[Spongebob Patrick Squidward Sandy #{0}] Traveling from {1} to {2} changed the state of the maze to:", ModuleId, characterUneditedNames[fromCharacter], characterUneditedNames[toCharacter]);
            for (int j = 0; j < 4; j++)
            {
                Debug.LogFormat("[Spongebob Patrick Squidward Sandy #{0}] {1} {2} {3} {4}", ModuleId, mazeArrows[0, j], mazeArrows[1, j], mazeArrows[2, j], mazeArrows[3, j]);
            }
        }

        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                string currentSpaceNewDirection = "";
                string otherSpaceNewDirection = "";
                Vector2Int otherSpace = new Vector2Int(0, 0);

                switch (mazeArrows[i, j])
                {
                    case '^':
                        otherSpace = new Vector2Int(i, j - 1);
                        currentSpaceNewDirection = "U";
                        otherSpaceNewDirection = "D";
                        break;
                    case '>':
                        otherSpace = new Vector2Int(i + 1, j);
                        currentSpaceNewDirection = "R";
                        otherSpaceNewDirection = "L";
                        break;
                    case 'v':
                        otherSpace = new Vector2Int(i, j + 1);
                        currentSpaceNewDirection = "D";
                        otherSpaceNewDirection = "U";
                        break;
                    case '<':
                        otherSpace = new Vector2Int(i - 1, j);
                        currentSpaceNewDirection = "L";
                        otherSpaceNewDirection = "R";
                        break;
                    default:
                        continue;
                }

                if (!maze[i, j].Contains(currentSpaceNewDirection))
                    maze[i, j] += currentSpaceNewDirection;
                if (!maze[otherSpace.x, otherSpace.y].Contains(otherSpaceNewDirection))
                    maze[otherSpace.x, otherSpace.y] += otherSpaceNewDirection;
            }
        }
        DebugMsg("Resulting maze without merging areas:");
        LogMaze();

        //finding maze areas and merging together if able
        int nextAreaIndex = 1;
        List<int> amountOfCellsInArea = new List<int>{ 0 };
        for(int i = 0; i < 4; i++)
        {
            for(int j = 0; j < 4; j++)
            {
                bool spaceHasIndex = mazeAreas[i, j] != 0;
                int spaceIndex = nextAreaIndex;
                if (spaceHasIndex)
                    spaceIndex = mazeAreas[i, j];
                int otherSpaceIndex = 0;

                int xOffset = 0;
                int yOffset = 0;
                switch (mazeArrows[i, j])
                {
                    case '^':
                        yOffset = -1;
                        break;
                    case '>':
                        xOffset = 1;
                        break;
                    case 'v':
                        yOffset = 1;
                        break;
                    case '<':
                        xOffset = -1;
                        break;
                    default:
                        continue;
                }

                otherSpaceIndex = mazeAreas[i + xOffset, j + yOffset];
                if (otherSpaceIndex != 0)
                {
                    if (spaceHasIndex)
                    {
                        if (spaceIndex == otherSpaceIndex)
                            continue;
                        for (int i2 = 0; i2 < 4; i2++)
                        {
                            for (int j2 = 0; j2 < 4; j2++)
                            {
                                if (mazeAreas[i2, j2] == otherSpaceIndex)
                                {
                                    mazeAreas[i2, j2] = spaceIndex;
                                    amountOfCellsInArea[spaceIndex]++;
                                    amountOfCellsInArea[otherSpaceIndex]--;
                                }
                            }
                        }
                        continue;
                    }
                    mazeAreas[i, j] = otherSpaceIndex;
                    amountOfCellsInArea[otherSpaceIndex]++;
                    continue;
                }
                if (spaceHasIndex)
                {
                    mazeAreas[i + xOffset, j + yOffset] = spaceIndex;
                    amountOfCellsInArea[spaceIndex]++;
                    continue;
                }
                mazeAreas[i, j] = nextAreaIndex++;
                mazeAreas[i + xOffset, j + yOffset] = mazeAreas[i, j];
                amountOfCellsInArea.Add(2);
                continue;
            }
        }
        for(int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                int spaceValue = mazeAreas[i, j];
                if (spaceValue == 0)
                    continue;

                if(j > 0)
                {
                    if(mazeAreas[i, j-1] > 0 && mazeAreas[i, j - 1] != spaceValue)
                    {
                        if (!maze[i, j].Contains("U"))
                            maze[i, j] += "U";
                        if (!maze[i, j-1].Contains("D"))
                            maze[i, j - 1] += "D";
                    }
                }
                if (j < 3)
                {
                    if (mazeAreas[i, j + 1] > 0 && mazeAreas[i, j + 1] != spaceValue)
                    {
                        if (!maze[i, j].Contains("D"))
                            maze[i, j] += "D";
                        if (!maze[i, j + 1].Contains("U"))
                            maze[i, j + 1] += "U";
                    }
                }
                if (i > 0)
                {
                    if (mazeAreas[i - 1, j] > 0 && mazeAreas[i - 1, j] != spaceValue)
                    {
                        if (!maze[i, j].Contains("L"))
                            maze[i, j] += "L";
                        if (!maze[i - 1, j].Contains("R"))
                            maze[i - 1, j] += "R";
                    }
                }
                if (i < 3)
                {
                    if (mazeAreas[i + 1, j] > 0 && mazeAreas[i + 1, j] != spaceValue)
                    {
                        if (!maze[i, j].Contains("R"))
                            maze[i, j] += "R";
                        if (!maze[i + 1, j].Contains("L"))
                            maze[i + 1, j] += "L";
                    }
                }
            }
        }
        //pruning outside areas. theres probably a better way to do this but i didnt care enough to think of any
        //college move in day is tomorrow and im up at 11:30 pm writing Spongebob Patrick Squidward Sandy code cut me some slack
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                int spaceIndex = mazeAreas[i, j];
                if (spaceIndex == 0)
                    continue;

                int otherSpaceIndex = 0;
                if(i > 0)
                {
                    otherSpaceIndex = mazeAreas[i - 1, j];
                    if(spaceIndex != otherSpaceIndex && otherSpaceIndex != 0)
                    {
                        for (int i2 = 0; i2 < 4; i2++)
                        {
                            for (int j2 = 0; j2 < 4; j2++)
                            {
                                if (mazeAreas[i2, j2] == otherSpaceIndex)
                                {
                                    mazeAreas[i2, j2] = spaceIndex;
                                    amountOfCellsInArea[spaceIndex]++;
                                    amountOfCellsInArea[otherSpaceIndex]--;
                                }
                            }
                        }
                    }
                }
                if (j > 0)
                {
                    otherSpaceIndex = mazeAreas[i, j - 1];
                    if (spaceIndex != otherSpaceIndex && otherSpaceIndex != 0)
                    {
                        for (int i2 = 0; i2 < 4; i2++)
                        {
                            for (int j2 = 0; j2 < 4; j2++)
                            {
                                if (mazeAreas[i2, j2] == otherSpaceIndex)
                                {
                                    mazeAreas[i2, j2] = spaceIndex;
                                    amountOfCellsInArea[spaceIndex]++;
                                    amountOfCellsInArea[otherSpaceIndex]--;
                                }
                            }
                        }
                    }
                }
            }
        }
        int largestMazeIndex = 1;
        for(int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                int index = mazeAreas[i, j];
                if (index == 0)
                    continue;

                if (index != largestMazeIndex && amountOfCellsInArea[index] > amountOfCellsInArea[largestMazeIndex])
                    largestMazeIndex = index;
            }
        }
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                if (mazeAreas[i, j] != largestMazeIndex)
                    maze[i, j] = ""; //we know that any index not a part of this is disconnected from every other index, so this is a safe thing to do
            }
        }
        DebugMsg("Resulting maze after merging areas and pruning disconnected areas:");
        LogMaze();

        int fewestNeighbors = 5;
        int mostNeighbors = 0;
        string goalPositionLog = "";
        int mazeSize = 16;
        for(int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                int neighbors = maze[j, i].Length; //neighbors are the opposite of walls- the more there are the more open the cell is
                int dummyi = i;
                int dummyj = j;

                if(neighbors == 0)
                {
                    mazeSize--;
                    continue;
                }

                if (neighbors < fewestNeighbors && neighbors > 0)
                {
                    fewestNeighbors = neighbors;
                    goalPositionLog = " (" + (dummyj + 1) + ", " + (dummyi + 1) + ")";
                    goalPositions.Clear();
                    goalPositions.Add(new Vector2Int(dummyj, dummyi));
                }
                else if (neighbors == fewestNeighbors)
                {
                    goalPositions.Add(new Vector2Int(dummyj, dummyi));
                    allGoalPositions.Add(new Vector2Int(dummyj, dummyi));
                    goalPositionLog += " (" + (dummyj + 1) + ", " + (dummyi + 1) + ")";
                }

                if(neighbors > mostNeighbors) //the for loops are made to go through the grid in reading order, so if the neighbors are the same is mostNeighbors there is no need to check
                {
                    mostNeighbors = neighbors;
                    initialPlayerPosition = new Vector2Int(dummyj, dummyi);
                    playerPosition = new Vector2Int(dummyj, dummyi);
                }
            }
        }
        travelledCells[playerPosition.x, playerPosition.y] = true;
        quota = Mathf.CeilToInt(mazeSize / 2f);

        DebugMsg("Your starting coordinate is (" + (playerPosition.x + 1) + ", " + (playerPosition.y + 1) + ").");
        DebugMsg("Your goal position(s) are" + goalPositionLog + ".");
        DebugMsg("The maze is " + mazeSize + " cells; the quota is " + quota + " cells.");

        string serial = bomb.GetSerialNumber();
        char[] alphabet = new char[26] { 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z' };
        List<int> serialLetterPositions = new List<int>();
        List<int> serialNumbers = new List<int>();
        for(int i = 0; i < 6; i++)
        {
            if (alphabet.Contains(serial[i]))
                serialLetterPositions.Add(System.Array.IndexOf(alphabet, serial[i]));
            else
                serialNumbers.Add(serial[i]);
        }

        switch(serialLetterPositions.Count)
        {
            case 2:
                List<int> sortedNumberPositions = new List<int>();
                for (int i = 0; i < 4; i++)
                    sortedNumberPositions.Add(serialNumbers[i]);
                sortedNumberPositions.Sort();
                for (int i = 3; i >= 0; i--)
                {
                    int index = 0;
                    for(int j = 3; j >= 0; j--)
                    {
                        if(serialNumbers[j] == sortedNumberPositions[i])
                        {
                            index = j;
                            break;
                        }
                    }
                    directionIndices[index] = 3 - i;
                    serialNumbers[index] = -1;
                }
                break;
            case 3:
                int currentButton = 0;
                List<int> unassignedButtons = new List<int> { 0, 1, 2, 3};
                for(int i = 0; i < 3; i++)
                {
                    if (serialNumbers[i] != 0)
                        currentButton = (currentButton + serialNumbers[i]) % unassignedButtons.Count;
                    else
                        currentButton = (currentButton + 2) % unassignedButtons.Count;
                    directionIndices[unassignedButtons[currentButton]] = i;
                    unassignedButtons.RemoveAt(currentButton);
                    currentButton--;
                }
                directionIndices[unassignedButtons[0]] = 3;
                break;
            case 4:
                List<int> sortedLetterPositions = new List<int>();
                for (int i = 0; i < 4; i++)
                    sortedLetterPositions.Add(serialLetterPositions[i]);
                sortedLetterPositions.Sort();
                for(int i = 0; i < 4; i++)
                {
                    int index = serialLetterPositions.IndexOf(sortedLetterPositions[i]);
                    directionIndices[index] = i;
                    serialLetterPositions[index] = -1;
                }
                break;
        }

        Debug.LogFormat("[Spongebob Patrick Squidward Sandy #{0}] The directions of the buttons in reading order are {1}, {2}, {3}, {4}.", ModuleId, directionNames[directionIndices[0]], directionNames[directionIndices[1]], directionNames[directionIndices[2]], directionNames[directionIndices[3]]);
    }

    void Awake()
    {
        ModuleId = ModuleIdCounter++;

        for(int i = 0; i < 4; i++)
        {
            int dummy = i;
            buttons[dummy].OnInteract += delegate () { buttonPressed(dummy); return false; };
        }
    }

    bool playOtherSound = true;
    void buttonPressed(int pressedButton)
    {
        buttons[pressedButton].AddInteractionPunch();
        bombAudio.PlayGameSoundAtTransformWithRef(KMSoundOverride.SoundEffect.ButtonPress, transform);
        switch(chosenCharacters[pressedButton])
        {
            case 0:
                bombAudio.PlaySoundAtTransform("spongebob", transform);
                break;
            case 1:
                bombAudio.PlaySoundAtTransform("patrick", transform);
                break;
            case 2:
                if (playOtherSound)
                    bombAudio.PlaySoundAtTransform("squidward", transform);
                else
                    bombAudio.PlaySoundAtTransform("squidward2", transform);
                playOtherSound = !playOtherSound;
                break;
            case 3:
                bombAudio.PlaySoundAtTransform("sandy", transform);
                break;
            case 4:
                bombAudio.PlaySoundAtTransform("larry", transform);
                break;
            case 5:
                bombAudio.PlaySoundAtTransform("pearl", transform);
                break;
            case 6:
                if(playOtherSound)
                    bombAudio.PlaySoundAtTransform("mr krabs", transform);
                else
                    bombAudio.PlaySoundAtTransform("mr krabs2", transform);
                playOtherSound = !playOtherSound;
                break;
            case 7:
                if (playOtherSound)
                    bombAudio.PlaySoundAtTransform("plankton", transform);
                else
                    bombAudio.PlaySoundAtTransform("plankton2", transform);
                playOtherSound = !playOtherSound;
                break;
            case 8:
                bombAudio.PlaySoundAtTransform("karen", transform);
                break;
            case 9:
                bombAudio.PlaySoundAtTransform("gary", transform);
                break;
            case 10:
                bombAudio.PlaySoundAtTransform("mrs puff", transform);
                break;
            case 11:
                bombAudio.PlaySoundAtTransform("bunchbob", transform);
                break;
            case 12:
                bombAudio.PlaySoundAtTransform("king neptune", transform);
                break;
        }

        if (moduleSolved)
        {
            return;
        }

        DebugMsg(characterUneditedNames[chosenCharacters[pressedButton]] + " (" + directionNames[directionIndices[pressedButton]] + ") has been pressed.");
        bool badMove = false;
        switch(directionIndices[pressedButton])
        {
            case 0:
                if (maze[playerPosition.x, playerPosition.y].Contains("U"))
                    playerPosition = new Vector2Int(playerPosition.x, playerPosition.y - 1);
                else
                    badMove = true;
                break;
            case 1:
                if (maze[playerPosition.x, playerPosition.y].Contains("R"))
                    playerPosition = new Vector2Int(playerPosition.x + 1, playerPosition.y);
                else
                    badMove = true;
                break;
            case 2:
                if (maze[playerPosition.x, playerPosition.y].Contains("D"))
                    playerPosition = new Vector2Int(playerPosition.x, playerPosition.y + 1);
                else
                    badMove = true;
                break;
            case 3:
                if (maze[playerPosition.x, playerPosition.y].Contains("L"))
                    playerPosition = new Vector2Int(playerPosition.x - 1, playerPosition.y);
                else
                    badMove = true;
                break;
        }
        if (badMove)
        {
            DebugMsg("Ran into a wall. Resetting the module.");
            module.HandleStrike();
            playerPosition = initialPlayerPosition;
            travelledCells = new bool[4, 4]
            {
                { false, false, false, false },
                { false, false, false, false },
                { false, false, false, false },
                { false, false, false, false }
            };
            travelledCells[initialPlayerPosition.x, initialPlayerPosition.y] = true;
            goalPositions.Clear();
            for (int i = 0; i < allGoalPositions.Count; i++)
            {
                int dummy = i;
                goalPositions.Add(allGoalPositions[dummy]);
            }
            return;
        }

        for(int i = 0; i < goalPositions.Count; i++)
        {
            if(goalPositions[i].x == playerPosition.x && goalPositions[i].y == playerPosition.y)
            {
                goalPositions.RemoveAt(i);
                break;
            }
        }
        if(!travelledCells[playerPosition.x, playerPosition.y])
        {
            travelledCells[playerPosition.x, playerPosition.y] = true;
            amountTravelled++;
        }
        if (amountTravelled >= quota && goalPositions.Count == 0)
        {
            DebugMsg("Both solve requirements have been met!");
            module.HandlePass();
            bombAudio.PlaySoundAtTransform("solve", transform);
            moduleSolved = true;
        }
    }

    Vector2Int positionInBigTable(int character, int position)
    {
        switch(character)
        {
            case 0:
                return new Vector2Int(0, 0);
            case 1:
                return new Vector2Int(2, 0);
            case 2:
                if (position < 2)
                    return new Vector2Int(4, 0);
                else
                    return new Vector2Int(2, 4);
            case 3:
                return new Vector2Int(6, 0);
            case 4:
                return new Vector2Int(0, 2);
            case 5:
                return new Vector2Int(2, 2);
            case 6:
                if (position < 2)
                    return new Vector2Int(4, 2);
                else
                    return new Vector2Int(4, 6);
            case 7:
                if (position < 2)
                    return new Vector2Int(6, 2);
                else
                    return new Vector2Int(0, 6);
            case 8:
                return new Vector2Int(0, 4);
            case 9:
                return new Vector2Int(4, 4);
            case 10:
                return new Vector2Int(6, 4);
            case 11:
                return new Vector2Int(2, 6);
            case 12:
                return new Vector2Int(6, 6);
        }
        return new Vector2Int(-1, -1);
    }

    Vector2Int positionInDiagram(int character, int encounters)
    {
        switch (character)
        {
            case 0:
                return new Vector2Int(0, 0);
            case 1:
                return new Vector2Int(1, 0);
            case 2:
                if (encounters % 2 == 0)
                    return new Vector2Int(2, 0);
                else
                    return new Vector2Int(1, 2);
            case 3:
                return new Vector2Int(3, 0);
            case 4:
                return new Vector2Int(0, 1);
            case 5:
                return new Vector2Int(1, 1);
            case 6:
                if (encounters % 2 == 0)
                    return new Vector2Int(2, 1);
                else
                    return new Vector2Int(2, 3);
            case 7:
                if (encounters % 2 == 0)
                    return new Vector2Int(3, 1);
                else
                    return new Vector2Int(0, 3);
            case 8:
                return new Vector2Int(0, 2);
            case 9:
                return new Vector2Int(2, 2);
            case 10:
                return new Vector2Int(3, 2);
            case 11:
                return new Vector2Int(1, 3);
            case 12:
                return new Vector2Int(3, 3);
        }
        return new Vector2Int(-1, -1);
    }

    Vector2Int Midpoint(Vector2Int a, Vector2Int b)
    {
        return new Vector2Int((a.x + b.x) / 2, (a.y + b.y) / 2);
    }

    void LogMaze()
    {
        char[] mazeSymbols = new char[16] { '.', '╙', '╒', '╚', '╖', '║', '╔', '╠', '╕', '╝', '═', '╩', '╗', '╣', '╦', '╬' };
        //sorted using binary numbers. up=1, right=2, down=4, left=8
        for(int i = 0; i < 4; i++)
        {
            string logRow = "";
            for (int j = 0; j < 4; j++)
            {
                logRow += mazeSymbols[
                    (maze[j, i].Contains("U") ? 1 : 0) +
                    (maze[j, i].Contains("R") ? 2 : 0) +
                    (maze[j, i].Contains("D") ? 4 : 0) +
                    (maze[j, i].Contains("L") ? 8 : 0)];
            }
            DebugMsg(logRow);
        }
    }

    private bool isCommandValid(string[] commands)
    {
        string[] regularCommands = new string[] { "press", "1", "2", "3", "4", "tl", "tr", "bl", "br" };
        string[] characterCommands = new string[] { "spongebob", "patrick", "squidward", "sandy", "larry", "pearl", "mrkrabs", "krabs", "plankton", "karen", "gary", "mrspuff", "mspuff", "puff", "bunchbob", "sadspongebob", "kingneptune", "neptune" };
        int[] characterCommandsIndices = new int[] { 0, 1, 2, 3, 4, 5, 6, 6, 7, 8, 9, 10, 10, 10, 11, 11, 12, 12};
        foreach(string cmd in commands)
        {
            if (regularCommands.Contains(cmd))
                continue;
            if(characterCommands.Contains(cmd))
            {
                if (chosenCharacters.Contains(characterCommandsIndices[System.Array.IndexOf(characterCommands, cmd)]))
                    continue;
                else
                    return false;
            }
            return false;
        }
        return true;
    }

    #pragma warning disable 414
    private readonly string TwitchHelpMessage = "Use !{0} press 1 2 3 4 / !{0} TL TR BL BR to press the buttons by their position. Use !{0} press spongebob patrick squidward sandy to press the buttons by their character. Use !{0} characters to see the list of valid characters.";
    #pragma warning restore 414
    IEnumerator ProcessTwitchCommand(string command)
    {
        if(command.EqualsIgnoreCase("characters"))
        {
            yield return "sendtochat The valid characters are spongebob, patrick, squidward, sandy, larry, pearl, mrkrabs / krabs, plankton, karen, gary, mrspuff / mspuff / puff, bunchbob / sadspongebob, kingneptune / neptune";
            yield break;
        }

        string[] commands = command.Trim().ToLowerInvariant().Split(new[] { ' ' });

        if (isCommandValid(commands))
        {
            yield return null;

            int currentStrikeNum = bomb.GetStrikes(); //im stealing this technique from exish

            foreach (string p in commands)
            {
                switch(p)
                {
                    case "1":
                    case "tl":
                        buttonPressed(0);
                        break;
                    case "2":
                    case "tr":
                        buttonPressed(1);
                        break;
                    case "3":
                    case "bl":
                        buttonPressed(2);
                        break;
                    case "4":
                    case "br":
                        buttonPressed(3);
                        break;
                    case "spongebob":
                        buttonPressed(System.Array.IndexOf(chosenCharacters, 0));
                        break;
                    case "patrick":
                        buttonPressed(System.Array.IndexOf(chosenCharacters, 1));
                        break;
                    case "squidward":
                        buttonPressed(System.Array.IndexOf(chosenCharacters, 2));
                        break;
                    case "sandy":
                        buttonPressed(System.Array.IndexOf(chosenCharacters, 3));
                        break;
                    case "larry":
                        buttonPressed(System.Array.IndexOf(chosenCharacters, 4));
                        break;
                    case "pearl":
                        buttonPressed(System.Array.IndexOf(chosenCharacters, 5));
                        break;
                    case "mrkrabs":
                    case "krabs":
                        buttonPressed(System.Array.IndexOf(chosenCharacters, 6));
                        break;
                    case "plankton":
                        buttonPressed(System.Array.IndexOf(chosenCharacters, 7));
                        break;
                    case "karen":
                        buttonPressed(System.Array.IndexOf(chosenCharacters, 8));
                        break;
                    case "gary":
                        buttonPressed(System.Array.IndexOf(chosenCharacters, 9));
                        break;
                    case "mrspuff":
                    case "mspuff":
                    case "puff":
                        buttonPressed(System.Array.IndexOf(chosenCharacters, 10));
                        break;
                    case "bunchbob":
                    case "sadspongebob":
                        buttonPressed(System.Array.IndexOf(chosenCharacters, 11));
                        break;
                    case "kingneptune":
                    case "neptune":
                        buttonPressed(System.Array.IndexOf(chosenCharacters, 12));
                        break;
                    default: //press
                        break;
                }

                if (bomb.GetStrikes() > currentStrikeNum)
                    yield break;

                yield return new WaitForSeconds(.5f);
            }
        }
        else
        {
            yield return "sendtochaterror One of the commands isn't recognized or you tried to press a character not on the module.";
        }
    }

    void DebugMsg(string msg)
    {
        Debug.LogFormat("[Spongebob Patrick Squidward Sandy #{0}] {1}", ModuleId, msg);
    }
}
