using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

namespace RandomPathGenerator{

    public enum SpawnerPlaceMode{
        OnCorner,
        OnBorder,
        RandomInCenter
    }
    public enum TowerPlaceMode{
        OnCorner,
        OnBorder,
        RandomInCenter,
        InCenter
    }

    public class RandomMapGenerator{

        public static int sizeX = 15;
        public static int sizeY = 15;
        public static int max_spawners = 5;
        public static SpawnerPlaceMode spawnersPlaceMode = SpawnerPlaceMode.RandomInCenter;
        public static TowerPlaceMode towerPlaceMode = TowerPlaceMode.RandomInCenter;
        public static int min_line_road = 1; // straight road for at least N blocks (if possible)
        public static int max_line_road = 3; // max

        public static int weaponFrequency = 7; // every N block of road. 0 = max fill

        public static bool spawnRivers = true;
        public static bool makeBridges = true;
        public static bool selfCrossRiver = true;
        public static int min_line_river = 1; // straight river for at least N blocks (if possible)
        public static int max_river_length = 20; // max
        public static int max_line_river = 5;
        public static float river_fill_rate = 30; // percent
        
        public static bool placeDecorations = true;
        public static float decorations_fill_rate = 5; // percent


        public static ConsoleColor GridBorder = ConsoleColor.White;
        public static ConsoleColor GridRoad = ConsoleColor.Green;
        public static ConsoleColor GridOtherRoad = ConsoleColor.DarkGreen;
        public static ConsoleColor GridTower = ConsoleColor.Cyan;
        public static ConsoleColor GridDefaultRoad = ConsoleColor.Yellow;
        public static ConsoleColor GridWeapon = ConsoleColor.Yellow;
        public static ConsoleColor GridSpawnerTower = ConsoleColor.Red;
        public static ConsoleColor GridSpawnerStart = ConsoleColor.Red;
        public static ConsoleColor GridBridge = ConsoleColor.Yellow;
        public static ConsoleColor GridRiver = ConsoleColor.Blue;
        public static ConsoleColor GridDecoration = ConsoleColor.DarkMagenta;
        public static ConsoleColor GridCoords = ConsoleColor.White;

        public static int[,] grid = new int[0,0];
        public static (int, int) TowerPos;
        public static (int,int) TowerEntrance;

        public static List<(int,int)> riversStart = new List<(int, int)>();

        public static int[] SpawnerValues = new int[]{6,7};

        public static void Main(string[]args){

            if(min_line_river > max_line_river || min_line_river <= 0){
                Console.WriteLine("wrong river parameter");
                return;
            }

            if(min_line_road > max_line_road || min_line_road <= 0){
                Console.WriteLine("wrong road parameter");
                return;
            }

            if(max_spawners < 1){
                Console.WriteLine("There must be at least 1 enemy spawner");
                return;    
            }

            if(sizeX < 7 || sizeY < 7 ||
              (towerPlaceMode == TowerPlaceMode.OnBorder && sizeX < 8 && sizeY < 8) ||
              ((towerPlaceMode == TowerPlaceMode.RandomInCenter || towerPlaceMode == TowerPlaceMode.InCenter) && (sizeX < 8 || sizeY < 8))){
                Console.WriteLine("wrong size");
                return;
            }

            List<(int,int,int)> spawnersPos;
            List<(int,int)> weaponPos;

            Stopwatch stopwatch1 = Stopwatch.StartNew();

            grid = new int[sizeX, sizeY];

            TowerPos = PlaceTower();
            spawnersPos = PlaceSpawners();
            weaponPos = PlaceWeapons();

            if(spawnRivers) riversStart = PlaceRivers();
            if(placeDecorations) PlaceDecorations();

            stopwatch1.Stop();

            TimeSpan elapsedTime1 = stopwatch1.Elapsed;
            Console.WriteLine($"Program execution time: {elapsedTime1}");

            if(!IsValidRoad(spawnersPos)){
                Console.WriteLine("No valid road");
                Console.WriteLine("spawners: " + spawnersPos.Count);
            }
            else{
                Console.WriteLine("spawners: " + spawnersPos.Count);
                Console.WriteLine("Rivers: " + riversStart.Count);
            }

            SetRoad();
            printGrid();

        }

        public static bool IsValidRoad(List<(int,int,int)> spawners){
            
            int count1 = 0;
            int count4 = 0;
            int count6 = 0;
            int count7 = 0;
            int count5 = 0;

            for(int i = 0;i<sizeY;i++){
                for(int j = 0;j<sizeX;j++){
                    if(grid[j,i] == 1) count1++;
                    
                    if(grid[j,i] == 5) count5++;
                    if(grid[j,i] == 4) count4++;
                    if(grid[j,i] == 6) count6++;
                    if(grid[j,i] == 7){
                        count7++;
                        int c2 = 0;
                        if(grid[j+1,i] == 2) c2++;
                        if(grid[j-1,i] == 2) c2++;
                        if(grid[j,i+1] == 2) c2++;
                        if(grid[j,i-1] == 2) c2++;
                        if(c2 > 1){ Console.WriteLine("spawner has 2 neighbour roads"); return false;}
                    }
                }
            }

            if(count5 <= 0){ Console.WriteLine("No weapons"); return false;}
            if(count1 != 22){ Console.WriteLine("tower error"); return false;}
            if(count4 != 3){ Console.WriteLine("tower road error"); return false;}
            if(count6 != spawners.Count){ Console.WriteLine("wrong spawners num"); return false;}
            if(count7 != spawners.Count){ Console.WriteLine("spawners num wrong"); return false;}

            foreach(var s in spawners){
                var p = (s.Item1, s.Item2);
                int roadNum = CountPaths(TowerEntrance.Item1, TowerEntrance.Item2, p.Item1,p.Item2);
                if(roadNum != 1){
                    Console.WriteLine("there is more than 1 road to tower");
                    return false;
                }
            }

            foreach(var r in riversStart){
                if(!IsValidRiver(r.Item1, r.Item2)){
                    Console.WriteLine("fail by river at (" + r.Item1 + " " + r.Item2 + ")");
                    return false;
                }
            }

            return true;

        }

        public static bool IsValidRiver(int startX, int startY){
            
            int length = 0;
            bool[,] visited = new bool[sizeX,sizeY];
            Queue<(int,int)> queue = new();

            queue.Enqueue((startX,startY));

            int[] dx = {0,1,0,-1};
            int[] dy = {1,0,-1,0};

            while(queue.Count > 0){
                
                length++;
                var curr = queue.Dequeue();

                int x = curr.Item1;
                int y = curr.Item2;

                if(!CanConnectRiver(x,y)) return false;

                for (int i = 0; i < 4; i++){

                    int nextX = x + dx[i];
                    int nextY = y + dy[i];

                    if(nextX<0 || nextY<0 || nextX >=sizeX || nextY>=sizeY) continue;

                    if(!visited[nextX, nextY] && IsRiverIndex(nextX,nextY)){
                        visited[nextX, nextY] = true;
                        queue.Enqueue((nextX,nextY));
                    }
                }
            }

            return true;

        }



        public static void PlaceDecorations(){
            
            int totalFree = 0;
            
            for(int i = 0;i<sizeX;i++){
                for(int j = 0;j<sizeY;j++){
                    if(grid[i,j] == 0)
                        totalFree++;
                }
            }

            int freeCount = totalFree;

            Random r = new Random();

            while((float)freeCount / totalFree * 100 > Math.Max(0, 100 - decorations_fill_rate)){

                int num = r.Next(freeCount - 1)+1;

                int c = 0;
                for(int index = 0;index < sizeX && c < num;index++){
                    for(int jndex = 0;jndex < sizeY;jndex++){
                        if(grid[index,jndex] == 0 && ++c == num){
                            grid[index,jndex] = 56;
                            freeCount--;
                            break;
                        }
                    }
                }
            }
        }



        public static int CountPaths(int startX, int startY, int targetX, int targetY)
        {
            
            if(startX<0 || startY<0 || startX >=sizeX || startY>=sizeY || (grid[startX, startY] != 2 &&grid[startX, startY] != 30 &&grid[startX, startY] != 31&& grid[startX, startY] != 3&& grid[startX, startY] != 7))
                return 0;

            if(grid[startX,startY] == 30){
                if(!IsRiverIndex(startX+1,startY) || !IsRiverIndex(startX-1,startY)){
                    Console.WriteLine("wrong river");
                }
            }
            if(grid[startX,startY] == 31){
                if(!IsRiverIndex(startX,startY+1) || !IsRiverIndex(startX,startY-1)){
                    Console.WriteLine("wrong river");
                }
            }

            if(startX == targetX && startY == targetY)
                return 1;

            int saveNum = grid[startX, startY];
            grid[startX, startY] = -1;

            int paths = CountPaths(startX - 1, startY, targetX, targetY) +
                        CountPaths(startX + 1, startY, targetX, targetY) +
                        CountPaths(startX, startY - 1, targetX, targetY) +
                        CountPaths(startX, startY + 1, targetX, targetY);

            grid[startX, startY] = saveNum;

            return paths;
        }










        public static int CountNeighbours(int[,] mask, int x,int y, params int[] n){
            return (x < sizeX - 1 && n.Contains(mask[x+1,y]) ? 1 : 0) +
                   (x > 0 &&n.Contains(mask[x-1,y]) ? 1 : 0) +
                   (y < sizeY - 1 && n.Contains(mask[x,y+1]) ? 1 : 0) +
                   (y > 0 && n.Contains(mask[x,y-1]) ? 1 : 0);
        }


        public static List<(int,int)> PlaceRivers(){

            List<(int,int)> pos = new List<(int, int)>();

            int[,] mask = new int[sizeX,sizeY];
            int freeCount = sizeX * sizeY;
            int bridgeCount = 0;

            for(int index = 0;index < sizeX;index++){
                for(int jndex = 0;jndex < sizeY;jndex++){
                    if(grid[index,jndex] != 0){
                        freeCount--;
                        if(makeBridges && IsValidForRiver(index,jndex)){
                            mask[index,jndex] = 1;
                            bridgeCount++;
                        }
                        else{
                            mask[index, jndex] = 2;
                        }
                    }
                }
            }

            int totalPlacedRivers = 0;
            int totalFree = freeCount;

            Random r = new Random();

            while((float)freeCount / totalFree * 100 > Math.Max(0, 100 - river_fill_rate)){

                for(int index = 0;index < sizeX;index++){
                    for(int jndex = 0;jndex < sizeY;jndex++){
                        if(mask[index,jndex] == 0 || mask[index,jndex] == 1){

                            if(CountNeighbours(mask, index,jndex, 3) >= 1){
                                freeCount-= mask[index,jndex] == 0 ? 1 : 0;
                                mask[index,jndex] = 2;
                                if(index > 0 && mask[index - 1,jndex] == 1) mask[index - 1,jndex] = 2;
                                if(index < sizeX - 1 && mask[index + 1,jndex] == 1) mask[index + 1,jndex] = 2;
                                if(jndex > 0 && mask[index,jndex - 1] == 1) mask[index,jndex - 1] = 2;
                                if(jndex < sizeY - 1 && mask[index,jndex + 1] == 1) mask[index,jndex + 1] = 2;
                            }
                        }
                    }
                }
                for(int index = 0;index < sizeX;index++){
                    for(int jndex = 0;jndex < sizeY;jndex++){
                        if(mask[index,jndex] == 0){

                            if(CountNeighbours(mask, index,jndex,0,1) == 0 || (CountNeighbours(mask, index,jndex,0,1) == 1 && (index == 0 || mask[index-1,jndex] == 2 || CountNeighbours(mask, index-1,jndex,0,1) < 2) &&
                            (index == sizeX - 1 || mask[index+1,jndex] == 2 || CountNeighbours(mask, index+1,jndex,0,1) < 2) &&
                            (jndex == 0 || mask[index,jndex-1] == 2 || CountNeighbours(mask, index,jndex-1,0,1) < 2) &&
                            (jndex == sizeY - 1 || mask[index,jndex+1] == 2 || CountNeighbours(mask, index,jndex+1,0,1) < 2))){
                                mask[index,jndex] = 2;
                                freeCount--;
                            }
                        }
                    }
                }


                if((float)freeCount / totalFree * 100 <= Math.Max(0, 100 - river_fill_rate)){
                    break;
                }

                int num = r.Next(freeCount - 1)+1;

                (int,int) riverStart = (-1,-1);

                int c = 0;
                for(int index = 0;index < sizeX && c < num;index++){
                    for(int jndex = 0;jndex < sizeY;jndex++){
                        if(mask[index,jndex] == 0){
                            if(++c == num){
                                riverStart = (index,jndex);
                                break;
                            }
                        }
                    }
                }

                pos.Add(riverStart);

                grid[riverStart.Item1, riverStart.Item2] = 8;
                mask[riverStart.Item1, riverStart.Item2] = 3;
                freeCount--;

                var riverStartCopy = riverStart;

                int riverLength = 1;
                int currStreak = r.Next(max_line_river - min_line_river+1) + min_line_river;

                var prevMove = (0,0);
                
                while(riverLength < Math.Max(max_river_length, 3)){

                    List<(int,int)> possibleNextList = new List<(int, int)>();

                    int x = riverStart.Item1;
                    int y = riverStart.Item2;

                    if(x < sizeX - 1 && (mask[x + 1, y] == 0 || mask[x + 1, y] == 1)) possibleNextList.Add((x+1,y));
                    if(y > 0 && (mask[x,y-1] == 0 || mask[x,y-1] == 1)) possibleNextList.Add((x,y-1));
                    if(x > 0 && (mask[x-1, y] == 0 || mask[x-1, y] == 1)) possibleNextList.Add((x-1,y));
                    if(y < sizeY - 1 && (mask[x,y+1] == 0 || mask[x,y+1] == 1)) possibleNextList.Add((x, y+1));

                    var nextCell = (-1,-1);

                    while(possibleNextList.Count > 0){

                        (int,int) possibleNextCell;

                        if(currStreak > 0 && possibleNextList.Contains((riverStart.Item1 + prevMove.Item1, riverStart.Item2 + prevMove.Item2))){
                            currStreak--;
                            possibleNextCell = (riverStart.Item1 + prevMove.Item1, riverStart.Item2 + prevMove.Item2);
                        }
                        else{
                            currStreak = r.Next(max_line_river);
                            possibleNextCell = possibleNextList[r.Next(possibleNextList.Count)];
                        }

                        if(!selfCrossRiver){
                            if(CountNeighbours(grid, possibleNextCell.Item1, possibleNextCell.Item2, 8,30,31) <= 1){
                                prevMove = (possibleNextCell.Item1 - riverStart.Item1, possibleNextCell.Item2 - riverStart.Item2);
                                nextCell = possibleNextCell;
                                break;
                            }

                        }
                        else{
                            if(CanConnectRiver(possibleNextCell.Item1, possibleNextCell.Item2)){
                                prevMove = (possibleNextCell.Item1 - riverStart.Item1, possibleNextCell.Item2 - riverStart.Item2);
                                nextCell = possibleNextCell;
                                if(prevMove.Item1 != 0){
                                    if(possibleNextCell.Item1 + prevMove.Item1 > 0 && possibleNextCell.Item1 + prevMove.Item1 < sizeX - 1 && grid[nextCell.Item1 + prevMove.Item1, nextCell.Item2] == 8){
                                        grid[nextCell.Item1,nextCell.Item2] = grid[nextCell.Item1,nextCell.Item2] == 2 ? 30 : 8;
                                        mask[nextCell.Item1,nextCell.Item2] = 3;
                                        nextCell = (possibleNextCell.Item1 + prevMove.Item1, possibleNextCell.Item2 + prevMove.Item2);
                                    }
                                }
                                else if(prevMove.Item2 != 0){
                                    if(possibleNextCell.Item2 + prevMove.Item2 > 0 && possibleNextCell.Item2 + prevMove.Item2 < sizeY - 1 && grid[nextCell.Item1, nextCell.Item2 + prevMove.Item2] == 8){
                                        grid[nextCell.Item1,nextCell.Item2] = grid[nextCell.Item1,nextCell.Item2] == 2 ? 31 : 8;
                                        mask[nextCell.Item1,nextCell.Item2] = 3;
                                        nextCell = (possibleNextCell.Item1 + prevMove.Item1, possibleNextCell.Item2 + prevMove.Item2);
                                    }
                                }
                                break;
                            }

                        }

                        possibleNextList.Remove(possibleNextCell);

                    }

                    if(nextCell == (-1,-1)){

                        if(riverLength < 3){
                            //printGrid();
                            nextCell = riverStart = riverStartCopy;
                            continue;
                        }
                        else{
                            break;
                        }
                    }

                    if(mask[nextCell.Item1, nextCell.Item2] == 1){

                        if(!HasDirectNeighbour(nextCell.Item1 * 2 - riverStart.Item1,nextCell.Item2 * 2 - riverStart.Item2, 8)){
                            grid[nextCell.Item1,nextCell.Item2] = (mask[nextCell.Item1 + 1, nextCell.Item2] == 0 || grid[nextCell.Item1 + 1, nextCell.Item2] == 8) ? 30 : 31;
                            mask[nextCell.Item1,nextCell.Item2] = 3;
                            nextCell = (nextCell.Item1 * 2 - riverStart.Item1,nextCell.Item2 * 2 - riverStart.Item2);
                            mask[nextCell.Item1,nextCell.Item2] = 3;
                            grid[nextCell.Item1,nextCell.Item2] = 8;
                            riverLength++;
                        }
                        else{
                            mask[nextCell.Item1,nextCell.Item2] = 2;
                            nextCell = riverStart;
                            continue;
                        }
                        
                    }
                    else{
                        mask[nextCell.Item1,nextCell.Item2] = 3;
                        grid[nextCell.Item1, nextCell.Item2] = 8;
                    }
                    riverStart = nextCell;

                    riverLength++;
                    freeCount--;

                }
                totalPlacedRivers += riverLength;
            
            }

            return pos;

        }

        public static bool CanConnectRiver(int x, int y){

            int rd = 0;
            int ru = 0;
            int ld = 0;
            int lu = 0;

            if(x < sizeX - 1){
                if(y < sizeY - 1){
                    if(grid[x+1,y+1] == 8){
                        rd++;
                    }
                }
                if(y > 0){
                    if(grid[x+1,y-1] == 8){
                        ru++;
                    }
                }
                if(grid[x+1,y] == 8){
                    rd++;
                    ru++;
                }
            }
            if(y > 0){
                if(x > 0){
                    if(grid[x-1, y-1] == 8)lu++;
                }
                if(grid[x,y-1] == 8){
                    ru++;
                    lu++;
                }
            }
            if(x > 0){
                if(y < sizeY - 1){
                    if(grid[x-1, y+1] == 8)ld++;
                }
                if(grid[x-1,y] == 8){
                    lu++;
                    ld++;
                }
            }
            if(y < sizeY - 1){
                if(grid[x,y+1] == 8){
                    rd++;
                    ld++;
                }
            }

            return((ru >= 0 && ru < 3) &&
                    (rd >= 0 && rd < 3) &&
                    (lu >= 0 && lu < 3) &&
                    (ld >= 0 && ld < 3));

        }

        public static bool IsValidForRiver(int x, int y){
            return grid[x,y] == 0 || (grid[x,y] == 2 && ((x > 0 && x < sizeX - 1 && grid[x-1, y] == 0 && grid[x+1,y] == 0) || (y > 0 && y < sizeY - 1 && grid[x, y-1] == 0 && grid[x,y+1] == 0)));
        }




        public static bool IsCuttingRoad(int x,int y){
            return ((x > 0 && x < sizeX - 1 && grid[x+1,y] == 2 && grid[x-1, y] == 2) || (y > 0 && y < sizeY - 1 && grid[x,y+1] == 2 && grid[x, y-1] == 2)) && grid[x,y] != 2;
        }



        public static bool HasPathSpawner(int startX, int startY, int targetX, int targetY, List<(int,int)> blockedCells, bool isFirstSpawner){

            bool[,] visited = new bool[sizeX, sizeY];

            if(!isFirstSpawner && (!CanConnectRoad(startX,startY, alternateMode: true) || (HasSpawnerNeighbour(startX,startY) && grid[startX,startY] != 2)) || IsCuttingRoad(startX,startY)){
                return false;
            }

            Queue<(int, int)> queue = new Queue<(int, int)>();

            visited[startX, startY] = true;

            queue.Enqueue((startX, startY));

            int[] dx = {0,1,0,-1};
            int[] dy = {1,0,-1,0};

            while (queue.Count > 0){

                var curr = queue.Dequeue();

                int x = curr.Item1;
                int y = curr.Item2;

                if(grid[x,y] == (isFirstSpawner ? 2 : 3) || grid[x,y] == 1) continue;

                if((isFirstSpawner && x == targetX && y == targetY))
                    return true;

                for (int i = 0; i < 4; i++){

                    int nextX = x + dx[i];
                    int nextY = y + dy[i];

                    if(!(nextX >= 0 && nextX < sizeX && nextY >= 0 && nextY < sizeY)) continue;

                    if(isFirstSpawner){
                        if(!HasNeighbourFromList(nextX,nextY, blockedCells) && !visited[nextX, nextY]){
                            visited[nextX, nextY] = true;
                            queue.Enqueue((nextX, nextY));
                        }
                    }
                    else{

                        if((grid[nextX, nextY] == 2 && (nextX == targetX && nextY == targetY || CanConnectRoad(nextX,nextY))) && !IsCuttingRoad(nextX,nextY))
                            return true;
/* 
                        Console.WriteLine("(" + nextX + " " + nextY +  ") Can connect: " + CanConnectRoad(nextX,nextY, alternateMode: true) + " BreakByNeighbour: " + (CountBreakingNeighbours(nextX, nextY, isFirstSpawner) < 1), ColImportant);
 */
                        if(!visited[nextX, nextY] && CanConnectRoad(nextX,nextY, alternateMode: true) && !IsCuttingRoad(nextX,nextY) && !HasNeighbourFromList(nextX,nextY, blockedCells) && CountBreakingNeighbours(nextX, nextY, isFirstSpawner) < 1){
                            visited[nextX, nextY] = true;
                            queue.Enqueue((nextX,nextY));
                        }
                    }
                }
            }

            return false;
        }
        
        public static bool HasPathRoad(int startX, int startY, int targetX, int targetY, (int,int) ignoreCell, List<(int,int)>? fantomRoad = null, bool isFirstSpawner = true){
            
            if(isFirstSpawner){
                if(startX < 0 || startX >= sizeX || startY < 0 || startY >= sizeY || HasDirectNeighbour(startX,startY, 7) || HasNeighbourRoad(startX, startY, ignoreCell, isFirstSpawner)){
                    return false;
                }
            }
            else{
                if(startX < 0 || startX >= sizeX || startY < 0 || startY >= sizeY || HasDirectNeighbour(startX,startY, 7) || IsCuttingRoad(startX,startY) || HasNeighbourRoad(startX,startY, ignoreCell, false) || (fantomRoad != null && HasNeighbourRoadList(fantomRoad)) || !CanConnectRoad(startX, startY, alternateMode: true)){
                    return false;
                }
            }

            bool[,] visited = new bool[sizeX, sizeY];

            Queue<((int, int),(int,int))> queue = new Queue<((int, int),(int,int))>();

            visited[startX, startY] = true;

            queue.Enqueue(((startX, startY), ignoreCell));

            int[] dx = {0,1,0,-1};
            int[] dy = {1,0,-1,0};

            while(queue.Count > 0){

                var curr = queue.Dequeue();

                int x = curr.Item1.Item1;
                int y = curr.Item1.Item2;

                if(SpawnerValues.Contains(grid[x,y]) || grid[x,y] == 1) continue;

                ignoreCell = curr.Item2;

                if((isFirstSpawner && x == targetX && y == targetY)){
                    return true;
                }

                for(int i = 0;i < 4;i++){

                    int nextX = x + dx[i];
                    int nextY = y + dy[i];

                    if(!(nextX >= 0 && nextX < sizeX && nextY >= 0 && nextY < sizeY)) continue;


                    if(isFirstSpawner){
                        /* m("(" + nextX + " " + nextY +  ") (nextx, nexty != ignoreCell): " + ((nextX,nextY) != ignoreCell) + " !HasNeighbourRoad: " + !HasNeighbourRoad(nextX, nextY, ignoreCell, true), ColImportant); */
                        if(!visited[nextX, nextY] && !HasDirectNeighbour(nextX,nextY, 7) && (fantomRoad == null || !HasNeighbourFromList(nextX, nextY, fantomRoad)) && !HasNeighbourRoad(nextX, nextY, ignoreCell, true)){
                            visited[nextX, nextY] = true;
                            queue.Enqueue(((nextX, nextY),(x,y)));
                        }
                    }
                    else{

                        if(grid[nextX, nextY] == 2 && (nextX == targetX && nextY == targetY || CanConnectRoad(nextX,nextY)) && !IsCuttingRoad(nextX,nextY))
                                return true;
/* 
                        m("(" + nextX + " " + nextY +  ") Valid: " + valid + " Can connect: " + CanConnectRoad(nextX,nextY, alternateMode: true), ColImportant);
 */
                        if(!visited[nextX, nextY] && CanConnectRoad(nextX,nextY, alternateMode: true) && !IsCuttingRoad(nextX,nextY) && CountBreakingNeighbours(nextX, nextY, isFirstSpawner) < 1 && (fantomRoad == null || !HasNeighbourFromList(nextX, nextY, fantomRoad))){
                            visited[nextX, nextY] = true;
                            queue.Enqueue(((nextX, nextY),(x,y)));
                        }
                    }

                }
            }
            return false;
        }

        public static bool HasNeighbourRoadList(List<(int,int)> pos){
            foreach(var p in pos){
                if(HasNeighbourRoad(p.Item1, p.Item2, (-1,-1), true)) return true;
            }
            return false;
        }

        public static bool HasNeighbourFromList(int x, int y, List<(int,int)> list){

            return x < sizeX - 1 && list.Contains((x + 1, y)) ||
                   x > 0 && list.Contains((x-1,y)) ||
                   y > 0 && list.Contains((x,y-1)) ||
                   y < sizeY - 1 && list.Contains((x,y+1));
        }

        public static bool HasNeighbourRoad(int x, int y, (int,int) ignore, bool isFirstSpawner){

            return (x < sizeX - 1 && grid[x+1,y] == (isFirstSpawner ? 2 : 3) && (x+1,y) != ignore) ||
                   (x > 0 && grid[x-1,y] == (isFirstSpawner ? 2 : 3)         && (x-1,y) != ignore) ||
                   (y > 0 && grid[x,y-1] == (isFirstSpawner ? 2 : 3)         && (x,y-1) != ignore) ||
                   (y < sizeY - 1 && grid[x,y+1] == (isFirstSpawner ? 2 : 3) && (x,y+1) != ignore);
        }

        public static int CountBreakingNeighbours(int x, int y, bool isFirstSpawner){
        
            return (x < sizeX - 1 && (grid[x+1,y] == (isFirstSpawner ? 2 : 3) || SpawnerValues.Contains(grid[x+1,y])) ? 1 : 0) +
                   (x > 0 && (grid[x-1,y] == (isFirstSpawner ? 2 : 3) || SpawnerValues.Contains(grid[x-1,y]))         ? 1 : 0) +
                   (y > 0 && (grid[x,y-1] == (isFirstSpawner ? 2 : 3) || SpawnerValues.Contains(grid[x,y-1]))         ? 1 : 0) +
                   (y < sizeY - 1 && (grid[x,y+1] == (isFirstSpawner ? 2 : 3) || SpawnerValues.Contains(grid[x,y+1])) ? 1 : 0);
        }
        public static bool HasSpawnerNeighbour(int x, int y){
        
            return (x < sizeX - 1 && SpawnerValues.Contains(grid[x+1,y])) ||
                   (x > 0 && SpawnerValues.Contains(grid[x-1,y])) ||
                   (y > 0 && SpawnerValues.Contains(grid[x,y-1])) ||
                   (y < sizeY - 1 && SpawnerValues.Contains(grid[x,y+1]));
        }



        public static bool IsValidSpawnerDir(int x, int y, int d){

            if(grid[x,y] != 0) return false;

            return d switch{
                1 => (x >= sizeX - 1 || grid[x + 1, y] == 0 || grid[x + 1, y] == 1) &&
                                        (y >= sizeY - 1 || grid[x, y + 1] == 0) &&
                                        (x <= 0 || grid[x - 1, y] == 0 || grid[x - 1, y] == 1) && !HasDirectNeighbour(x, y + 1, 7),
                2 => (y <= 0 || grid[x, y - 1] == 0 || grid[x, y - 1] == 1) &&
                                        (y >= sizeY - 1 || grid[x, y + 1] == 0 || grid[x, y + 1] == 1) &&
                                        (x <= 0 || grid[x - 1, y] == 0) && !HasDirectNeighbour(x - 1, y, 7),
                3 => (x >= sizeX - 1 || grid[x + 1, y] == 0 || grid[x + 1, y] == 1) &&
                                        (y <= 0 || grid[x, y - 1] == 0) &&
                                        (x <= 0 || grid[x - 1, y] == 0 || grid[x - 1, y] == 1) && !HasDirectNeighbour(x, y - 1, 7),
                4 => (x >= sizeX - 1 || grid[x + 1, y] == 0) &&
                                        (y <= 0 || grid[x, y - 1] == 0 || grid[x, y - 1] == 1) &&
                                        (y >= sizeY - 1 || grid[x, y + 1] == 0 || grid[x, y + 1] == 1) && !HasDirectNeighbour(x + 1, y, 7),
                _ => false,
            };
        }

        public static bool HasNeighbour(int x,int y, params int[] n){
            return x < sizeX - 1 && y < sizeY - 1 && n.Contains(grid[x+1,y+1]) ||
                   x < sizeX - 1 && y > 0 && n.Contains(grid[x+1,y-1]) ||
                   x > 0 && y < sizeY - 1 && n.Contains(grid[x-1,y+1]) ||
                   x > 0 && y > 0 && n.Contains(grid[x-1,y-1]) ||
                   x < sizeX - 1 && n.Contains(grid[x+1,y]) ||
                   x > 0 &&n.Contains(grid[x-1,y]) ||
                   y < sizeY - 1 && n.Contains(grid[x,y+1]) ||
                   y > 0 && n.Contains(grid[x,y-1]);
        }
        public static bool HasDirectNeighbour(int x,int y, params int[] n){
            return x < sizeX - 1 && n.Contains(grid[x+1,y]) ||
                   x > 0 &&n.Contains(grid[x-1,y]) ||
                   y < sizeY - 1 && n.Contains(grid[x,y+1]) ||
                   y > 0 && n.Contains(grid[x,y-1]);
        }

        public static int ChooseSpawnerDir(int x, int y, List<int>dir, int spawnerNum){

            Random r = new Random();
            
            while(dir.Count > 0){
                
                int n =  r.Next(dir.Count);
                List<(int,int)> blockedCells = new List<(int, int)>();

                int cx = x;
                int cy = y;

                blockedCells.Add((x,y));

                switch(dir[n]){
                    case 1:
                        blockedCells.Add((x,y+1)); cy--; break;
                    case 2:
                        blockedCells.Add((x-1,y)); cx++; break;
                    case 3:
                        blockedCells.Add((x,y-1)); cy++; break;
                    case 4:
                        blockedCells.Add((x+1,y)); cx--; break;
                }

                if(IsValidSpawnerDir(x,y,dir[n]) && HasPathSpawner(cx,cy,TowerEntrance.Item1, TowerEntrance.Item2, blockedCells, spawnerNum == 0))
                    return dir[n];

                dir.RemoveAt(n);
            }

            return 0;

        }

        public static List<(int,int)> PlaceWeapons(){

            List<(int,int)> pos = new List<(int, int)>();
            
            WeaponCycle(TowerEntrance.Item1,TowerEntrance.Item2,(-1,-1), weaponFrequency, pos);

            return pos;
        }


        public static void WeaponCycle(int x, int y,(int,int) ignore, int c, List<(int,int)> pos){

            //Console.WriteLine("pos: (" + x + " " + y + ")");

            if(c >= weaponFrequency){
                if(weaponFrequency == 0){
                    foreach(var p in FindFreeNeighbours(x,y)){
                        pos.Add(p);
                        grid[p.Item1, p.Item2] = 5;
                    }
                }
                else{
                    var weaponPos = FindFreeNeighbours(x,y);

                    if(weaponPos.Count > 0){

                        Random r = new Random();
                        var nextWeaponPos = weaponPos[r.Next(weaponPos.Count)];

                        c = 0;
                        pos.Add(nextWeaponPos);
                        grid[nextWeaponPos.Item1, nextWeaponPos.Item2] = 5;

                    }

                }
            }

            if(x < sizeX - 1 && grid[x+1,y] == 2 && (x+1,y) != ignore) WeaponCycle(x+1,y,(x,y), c+1, pos);
            if(x > 0 && grid[x-1,y] == 2 && (x-1,y) != ignore) WeaponCycle(x-1,y,(x,y), c+1, pos);
            if(y < sizeY - 1 && grid[x,y+1] == 2 && (x,y+1) != ignore) WeaponCycle(x,y+1,(x,y), c+1, pos);
            if(y > 0 && grid[x,y-1] == 2 && (x,y-1) != ignore) WeaponCycle(x,y-1,(x,y), c+1, pos);

        }

        public static List<(int,int)> FindFreeNeighbours(int x, int y){

            List<(int,int)> posisble = new List<(int, int)>();

            if(x < sizeX - 1){
                if(y > 0 && grid[x + 1, y - 1] == 0) posisble.Add((x+1,y-1));
                if(y < sizeY - 1 && grid[x + 1, y + 1] == 0) posisble.Add((x+1,y+1));
                if(grid[x + 1, y] == 0) posisble.Add((x+1,y));
            }
            if(y > 0){
                if(x > 0 && grid[x-1,y-1] == 0) posisble.Add((x-1,y-1));
                if(grid[x,y-1] == 0) posisble.Add((x,y-1));
            }
            if(x > 0){
                if(y < sizeY - 1 && grid[x-1, y+1] == 0) posisble.Add((x-1,y+1));
                if(grid[x-1, y] == 0) posisble.Add((x-1,y));
            }
            if(y < sizeY - 1 && grid[x,y+1] == 0) posisble.Add((x, y+1));

            return posisble;
        }

        public static List<(int,int,int)> PlaceSpawners(){

            List<(int,int,int)> pos = new List<(int, int, int)>();

            Random random = new Random();

            bool[,] isTaken = new bool[sizeX - 2, sizeY - 2];
            int emptyPos = (sizeX - 2) * (sizeY - 2);
            

            int spawnerX;
            int spawnerY;
            int spawnerDir = 0;
            int spawnerStartX = 0;
            int spawnerStartY = 0;

            List<int> dirs;

            List<(int, int, int)> corners = new List<(int, int, int)>(
                new (int,int,int)[]{
                    (0, 1, 1),
                    (1, sizeX-2, 1),
                    (2, 1, sizeY - 2),
                    (3, sizeX-2, sizeY - 2)
                }
            );

            List<int> PossiblePos = Enumerable.Range(1, 2 * sizeX + 2 * sizeY - 12).ToList();

            for(int i = 0, j = 0;i<max_spawners;i++,j++){

                switch(spawnersPlaceMode){
                    case SpawnerPlaceMode.OnCorner:
                        if(corners.Count == 0){
                            Console.WriteLine($"cannot place spawner {i + 1}");
                            return pos;
                        }

                        int corner = (j == 0 && towerPlaceMode == TowerPlaceMode.OnCorner && corners.Count >= 3) ? 3 : random.Next(corners.Count);

                        dirs = corners[corner].Item1 switch{
                            0 => new List<int>(new int[] { 2, 3 }),
                            1 => new List<int>(new int[] { 3, 4 }),
                            2 => new List<int>(new int[] { 1, 2 }),
                            3 => new List<int>(new int[] { 1, 4 }),
                            _ => new List<int>(),
                        };

                        spawnerX = corners[corner].Item2;
                        spawnerY = corners[corner].Item3;

                        spawnerStartX = spawnerX;
                        spawnerStartY = spawnerY;

                        spawnerDir = ChooseSpawnerDir(spawnerX, spawnerY, dirs, pos.Count);

                        switch(spawnerDir){
                            case 1:
                                spawnerStartY--;
                                grid[spawnerX, spawnerY + 1] = 6;
                                break;
                            case 2:
                                spawnerStartX++;
                                grid[spawnerX - 1, spawnerY] = 6;
                                break;
                            case 3:
                                spawnerStartY++;
                                grid[spawnerX, spawnerY - 1] = 6;
                                break;
                            case 4:
                                spawnerStartX--;
                                grid[spawnerX + 1, spawnerY] = 6;
                                break;
                            default:
                                corners.RemoveAt(corner);
                                i--;
                                continue;
                        }

                        grid[spawnerX, spawnerY] = 7;
                        pos.Add((spawnerX, spawnerY, spawnerDir));
                        corners.RemoveAt(corner);
                        BuildPath(spawnerStartX, spawnerStartY, TowerEntrance.Item1, TowerEntrance.Item2, spawnerDir, pos.Count == 1);
                        break;

                    case SpawnerPlaceMode.OnBorder:

                        if(PossiblePos.Count == 0){
                            return pos;
                        }

                        int n = PossiblePos[random.Next(PossiblePos.Count)];


                        dirs = new List<int>(new int[]{1,2,3,4});
                        if(n <= sizeX - 2){
                            spawnerX = n;
                            spawnerY = 1;
                        }
                        else if(n <= sizeX + sizeY - 5){
                            spawnerX = sizeX - 2;
                            spawnerY = n - sizeX + 3;
                        }
                        else if(n <= sizeX * 2 + sizeY - 8){
                            spawnerX = sizeX * 2 + sizeY - 7 - n;
                            spawnerY = sizeY - 2;
                        }
                        else{
                            spawnerX = 1;
                            spawnerY = sizeX * 2 + sizeY * 2 - 10 - n;
                        }

                        spawnerStartX = spawnerX;
                        spawnerStartY = spawnerY;

                        spawnerDir = ChooseSpawnerDir(spawnerX, spawnerY, dirs, pos.Count);

                        switch(spawnerDir){
                            case 1:
                                spawnerStartY--;
                                grid[spawnerX, spawnerY + 1] = 6;
                                break;
                            case 2:
                                spawnerStartX++;
                                grid[spawnerX - 1, spawnerY] = 6;
                                break;
                            case 3:
                                spawnerStartY++;
                                grid[spawnerX, spawnerY - 1] = 6;
                                break;
                            case 4:
                                spawnerStartX--;
                                grid[spawnerX + 1, spawnerY] = 6;
                                break;
                            default:
                                PossiblePos.Remove(n);
                                i--;
                                continue;
                        }

                        grid[spawnerX, spawnerY] = 7;
                        pos.Add((spawnerX, spawnerY, spawnerDir));
                        PossiblePos.Remove(n);
                        BuildPath(spawnerStartX, spawnerStartY, TowerEntrance.Item1, TowerEntrance.Item2, spawnerDir, pos.Count == 1);

                        break;

                    case SpawnerPlaceMode.RandomInCenter:

                        if(emptyPos == 0){
                            return pos;
                        }

                        spawnerX = 0;
                        spawnerY = 0;

                        int num = random.Next(emptyPos-1)+1;

                        int c = 0;
                        for(int index = 0;index < sizeX - 2 && c < num;index++){
                            for(int jndex = 0;jndex < sizeY - 2;jndex++){
                                if(!isTaken[index,jndex]){
                                    if(++c == num){
                                        spawnerX = index+1;
                                        spawnerY = jndex+1;
                                        break;
                                    }
                                }
                            }
                        }


                        dirs = new List<int>(new int[]{1,2,3,4});

                        spawnerStartX = spawnerX;
                        spawnerStartY = spawnerY;

                        spawnerDir = ChooseSpawnerDir(spawnerX, spawnerY, dirs, pos.Count);

                        switch(spawnerDir){
                            case 1:
                                if(spawnerY - 2 >= 0 && !isTaken[spawnerX-1, spawnerY - 2]){emptyPos--;  isTaken[spawnerX-1, spawnerY - 2] = true;}
                                if(spawnerY < sizeY-3 && !isTaken[spawnerX-1, spawnerY]){emptyPos--;  isTaken[spawnerX-1, spawnerY] = true;}
                                spawnerStartY--;
                                grid[spawnerX, spawnerY + 1] = 6;
                                break;
                            case 2:
                                if(spawnerX - 2 >= 0 && !isTaken[spawnerX - 2, spawnerY-1]){emptyPos--;  isTaken[spawnerX - 2, spawnerY-1] = true;}
                                if(spawnerX < sizeX-3 && !isTaken[spawnerX, spawnerY-1]){emptyPos--;  isTaken[spawnerX, spawnerY-1] = true;}
                                spawnerStartX++;
                                grid[spawnerX - 1, spawnerY] = 6;
                                break;
                            case 3:
                                if(spawnerY - 2 >= 0 && !isTaken[spawnerX-1, spawnerY - 2]){emptyPos--;  isTaken[spawnerX-1, spawnerY - 2] = true;}
                                if(spawnerY < sizeY-3 && !isTaken[spawnerX-1, spawnerY]){emptyPos--;  isTaken[spawnerX-1, spawnerY] = true;}
                                spawnerStartY++;
                                grid[spawnerX, spawnerY - 1] = 6;
                                break;
                            case 4:
                                if(spawnerX - 2 >= 0 && !isTaken[spawnerX - 2, spawnerY-1]){emptyPos--;  isTaken[spawnerX - 2, spawnerY-1] = true;}
                                if(spawnerX < sizeX-3 && !isTaken[spawnerX, spawnerY-1]){emptyPos--;  isTaken[spawnerX, spawnerY-1] = true;}
                                spawnerStartX--;
                                grid[spawnerX + 1, spawnerY] = 6;
                                break;
                            default:
                                emptyPos--;
                                isTaken[spawnerX - 1, spawnerY - 1] = true;
                                i--;
                                continue;
                        }

                        grid[spawnerX, spawnerY] = 7;
                        emptyPos--;
                        isTaken[spawnerX - 1, spawnerY - 1] = true;
                        pos.Add((spawnerX, spawnerY, spawnerDir));
                        BuildPath(spawnerStartX, spawnerStartY, TowerEntrance.Item1, TowerEntrance.Item2, spawnerDir, pos.Count == 1);

                        break;

                }



            }

            return pos;

        }

        public static bool CanConnectRoad(int x,int y, bool alternateMode = false){
            
            int rd = 0;
            int ru = 0;
            int ld = 0;
            int lu = 0;

            int direct = 0;

            if(x < sizeX - 1){
                if(y < sizeY - 1){
                    if(grid[x+1,y+1] == 2){
                        rd++;
                    }
                }
                if(y > 0){
                    if(grid[x+1,y-1] == 2){
                        ru++;
                    }
                }
                if(grid[x+1,y] == 2){
                    rd++;
                    ru++;
                    direct++;
                }
            }
            if(y > 0){
                if(x > 0){
                    if(grid[x-1, y-1] == 2)lu++;
                }
                if(grid[x,y-1] == 2){
                    ru++;
                    lu++;
                    direct++;
                }
            }
            if(x > 0){
                if(y < sizeY - 1){
                    if(grid[x-1, y+1] == 2)ld++;
                }
                if(grid[x-1,y] == 2){
                    lu++;
                    ld++;
                    direct++;
                }
            }
            if(y < sizeY - 1){
                if(grid[x,y+1] == 2){
                    rd++;
                    ld++;
                    direct++;
                }
            }

            int count1 = (ru == 1 ? 1 : 0) +
                            (rd == 1 ? 1 : 0) +
                            (lu == 1 ? 1 : 0) +
                            (ld == 1 ? 1 : 0);

            int count2 = (ru == 2 ? 1 : 0) +
                            (rd == 2 ? 1 : 0) +
                            (lu == 2 ? 1 : 0) +
                            (ld == 2 ? 1 : 0);

            if(count1 == 2 && count2 == 2 && grid[x,y] != 2) return false;

            if(alternateMode){
                return ru >= 0 && ru < 3 &&
                       rd >= 0 && rd < 3 &&
                       lu >= 0 && lu < 3 &&
                       ld >= 0 && ld < 3;
            }
            else{
                
                return((ru >= 1 && ru < 3) ||
                       (rd >= 1 && rd < 3) ||
                       (lu >= 1 && lu < 3) ||
                       (ld >= 1 && ld < 3)) && direct > 0;
            }

        }

        public static void Replace2On3(int sx,int sy){

            Queue<(int,int)> q = new Queue<(int, int)>();

            q.Enqueue((sx,sy));

            while(q.Count > 0){
                var p = q.Dequeue();
                
                int x = p.Item1;
                int y = p.Item2;

                grid[x,y] = 2;
                if(x < sizeX - 1 && grid[x+1,y] == 3) q.Enqueue((x+1,y));
                if(x >= 1 && grid[x-1,y] == 3) q.Enqueue((x-1,y));
                if(y < sizeY - 1 && grid[x,y+1] == 3) q.Enqueue((x,y+1));
                if(y >= 1 && grid[x,y-1] == 3) q.Enqueue((x,y-1));
            }
        }

        public static void BuildPath(int startX, int startY, int targetX, int targetY, int direction, bool isFirstSpawner){

            bool reached = false;

            if(HasNeighbourRoad(startX,startY, (-1,-1), isFirstSpawner: true) || (startX == targetX && startY == targetY)){
                grid[startX, startY] = 2;
                return;
            }

            int startXCopy = startX;
            int startYCopy = startY;

            grid[startX, startY] = isFirstSpawner ? 2 : 3;

            List<(int,int)> AllDirs = new List<(int, int)>(){
                (0,1),(-1,0),(0,-1),(1,0)
            };

            Random r = new Random();
            List<(int,int)> fantomRoad;
            int currLength;

            List<(int,int)> dir = new List<(int,int)>(AllDirs);
            dir.RemoveAt(direction - 1);

            while(!reached && dir.Count > 0){

                (int, int) currDir = dir[r.Next(dir.Count)];

                if(!HasPathRoad(startX + currDir.Item1, startY + currDir.Item2, TowerEntrance.Item1, TowerEntrance.Item2, (startX, startY), isFirstSpawner: isFirstSpawner)){
                    dir.Remove(currDir);
                    continue;
                }

                fantomRoad = new List<(int, int)>(){(startX + currDir.Item1, startY + currDir.Item2)};

                currLength = 2;
                
                int LineLength = r.Next(max_line_road-min_line_road+1)+min_line_road;

                while(currLength <= LineLength && HasPathRoad(startX + currLength * currDir.Item1, startY + currLength * currDir.Item2, TowerEntrance.Item1, TowerEntrance.Item2, (startX + currLength * currDir.Item1 + currDir.Item1 * -1, startY + currLength * currDir.Item2 + currDir.Item2 * -1), fantomRoad, isFirstSpawner)){
                    currLength++;
                    fantomRoad.Add((startX + currLength * currDir.Item1 + currDir.Item1 * -1, startY + currLength * currDir.Item2 + currDir.Item2 * -1));
                }

                currLength = Math.Min(currLength - 1, LineLength);

                for(int i = 0;i<=currLength;i++){
                    grid[startX + i * currDir.Item1, startY + i * currDir.Item2] = isFirstSpawner ? 2 : 3;
                }

                startX += currLength * currDir.Item1;
                startY += currLength * currDir.Item2;

                if(!isFirstSpawner && (CanConnectRoad(startX, startY))){
                    reached = true;
                }

                if(isFirstSpawner){
                    if((startX, startY) == TowerEntrance){
                        reached = true;
                    }
                }

                dir = new List<(int,int)>(AllDirs);
                dir.Remove((currDir.Item1 * -1, currDir.Item2 * -1));

            }

            if(!isFirstSpawner) Replace2On3(startXCopy, startYCopy);
            
        }

        public static (int,int) PlaceTower(){

            int posx = 0;
            int posy = 0;

            Random r = new();

            switch(towerPlaceMode){
                case TowerPlaceMode.OnCorner:
                    posx = posy = 2;
                    for(int i = posx - 2;i<posx+3;i++){
                        for(int j = posy - 2; j < posy+3;j++){
                            grid[i,j] = 1;
                        }
                    }
                    break;

                case TowerPlaceMode.OnBorder:

                    int coord = r.Next(sizeX + sizeY - 9);

                    posy = sizeY - coord - 5;
                    posx = posy < 0 ? coord - sizeY + 5 : 0;
                    posy = posy < 0 ? 0 : posy;

                    for(int i = posx;i<posx+5;i++){
                        for(int j = posy; j < posy+5;j++){
                            grid[i,j] = 1;
                        }
                    }

                    posx +=2;
                    posy +=2;

                    break;

                case TowerPlaceMode.RandomInCenter:

                    posy = r.Next(sizeY-4);
                    posx = r.Next(sizeX-4);

                    for(int i = posx;i<posx+5;i++){
                        for(int j = posy; j < posy+5;j++){
                            grid[i,j] = 1;
                        }
                    }

                    posx +=2;
                    posy +=2;

                    break;

                case TowerPlaceMode.InCenter:

                    posy = (sizeY >> 1) - 2;
                    posx = (sizeX >> 1) - 2;

                    //Console.WriteLine(posy + ", " + posx +" Tower");

                    for(int i = posx;i<posx+5;i++){
                        for(int j = posy; j < posy+5;j++){
                            grid[i,j] = 1;
                        }
                    }

                    posx +=2;
                    posy +=2;

                    break;
            }

            int dir = ChooseTowerDir(posx, posy);
            switch(dir){
                case 1:
                    for(int i = 0;i<4;i++)
                        grid[posx, posy - i] = 4;
                    TowerEntrance.Item1 = posx;
                    TowerEntrance.Item2 = posy - 3;
                    break;
                case 2:
                    for(int i = 0;i<4;i++)
                        grid[posx + i, posy] = 4;
                    TowerEntrance.Item1 = posx + 3;
                    TowerEntrance.Item2 = posy;
                    break;
                case 3:
                    for(int i = 0;i<4;i++)
                        grid[posx, posy + i] = 4;
                    TowerEntrance.Item1 = posx;
                    TowerEntrance.Item2 = posy + 3;
                    break;
                case 4:
                    for(int i = 0;i<4;i++)
                        grid[posx - i, posy] = 4;
                    TowerEntrance.Item1 = posx - 3;
                    TowerEntrance.Item2 = posy;
                    break;
                default:
                    break;
            }
            return (posx, posy);
        }

        public static int ChooseTowerDir(int x, int y){

            List<int> dir = new List<int>(new int[]{1,2,3,4});
            Random r = new Random();
            while(dir.Count > 0){
                int n = r.Next(dir.Count);
                if(IsValidTowerDir(x,y,dir[n])){
                    return dir[n];
                }
                dir.RemoveAt(n);
            }
            return 0;

        }

        public static bool IsValidTowerDir(int x, int y, int d){
            return d switch{
                1 => y > 2,
                2 => x < sizeX - 3,
                3 => y < sizeY - 3,
                4 => x > 2,
                _ => false,
            };
        }

        public static void printGrid(){

            Console.ForegroundColor = GridCoords;
            Console.Write("    ");
            for(int i = 0;i<sizeX;i++){
                Console.Write(i%10 + " ");
            }
            Console.WriteLine();
            Console.Write("  ");
            Console.ForegroundColor = GridBorder;
            for(int i = 0;i<sizeX + 1;i++){
                Console.Write("##");
            }
            Console.Write("#");
            Console.ResetColor();
            Console.WriteLine();
            for(int i = 0;i<sizeY;i++){
                Console.ForegroundColor = GridCoords;
                Console.Write(i%10);
                Console.ForegroundColor = GridBorder;
                Console.Write(" # ");
                for(int j = 0;j<sizeX;j++){

                    switch(grid[j,i]){
                        case 0: Console.Write("  "); break;
                        case 1: Console.ForegroundColor = GridTower; Console.Write("* "); break;
                        case 2: Console.ForegroundColor = GridRoad; Console.Write(grid[j,i] + " "); break;
                        case 3: Console.ForegroundColor = GridOtherRoad; Console.Write(grid[j,i] + " "); break;
                        case 4: Console.ForegroundColor = GridDefaultRoad; Console.Write(grid[j,i] + " "); break;
                        case 5: Console.ForegroundColor = GridWeapon; Console.Write("X "); break;
                        case 6: Console.ForegroundColor = GridSpawnerTower; Console.Write("S "); break;
                        case 7: Console.ForegroundColor = GridSpawnerStart; Console.Write(grid[j,i] + " "); break;
                        case 8: Console.ForegroundColor = GridRiver; Console.Write("R "); break;
                        case 10: Console.ForegroundColor = GridRoad; Console.Write("▲ "); break;
                        case 11: Console.ForegroundColor = GridRoad; Console.Write("►═"); break;
                        case 12: Console.ForegroundColor = GridRoad; Console.Write("▼ "); break;
                        case 13: Console.ForegroundColor = GridRoad; Console.Write("◄ "); break;
                        case 14: Console.ForegroundColor = GridRoad; Console.Write("║ "); break;
                        case 15: Console.ForegroundColor = GridRoad; Console.Write("══"); break;
                        case 16: Console.ForegroundColor = GridRoad; Console.Write("╚═"); break;
                        case 17: Console.ForegroundColor = GridRoad; Console.Write("╔═"); break;
                        case 18: Console.ForegroundColor = GridRoad; Console.Write("╗ "); break;
                        case 19: Console.ForegroundColor = GridRoad; Console.Write("╝ "); break;
                        case 20: Console.ForegroundColor = GridRoad; Console.Write("╩═"); break;
                        case 21: Console.ForegroundColor = GridRoad; Console.Write("╠═"); break;
                        case 22: Console.ForegroundColor = GridRoad; Console.Write("╦═"); break;
                        case 23: Console.ForegroundColor = GridRoad; Console.Write("╣ "); break;
                        case 24: Console.ForegroundColor = GridRoad; Console.Write("╬═"); break;
                        case 25: Console.ForegroundColor = GridRoad; Console.Write("· "); break;
                        case 26: Console.ForegroundColor = GridSpawnerStart; Console.Write("▲ "); break;
                        case 27: Console.ForegroundColor = GridSpawnerStart; Console.Write("►"); Console.ForegroundColor = GridRoad; Console.Write("═"); break;
                        case 28: Console.ForegroundColor = GridSpawnerStart; Console.Write("▼ "); break;
                        case 29: Console.ForegroundColor = GridSpawnerStart; Console.Write("◄ "); break;
                        case 30: Console.ForegroundColor = GridBridge; Console.Write("║"); Console.ForegroundColor = GridRiver; Console.Write("═");break;
                        case 31: Console.ForegroundColor = GridBridge; Console.Write("══"); break;

                        case 40: Console.ForegroundColor = GridRiver; Console.Write("▲ "); break;
                        case 41: Console.ForegroundColor = GridRiver; Console.Write("►═"); break;
                        case 42: Console.ForegroundColor = GridRiver; Console.Write("▼ "); break;
                        case 43: Console.ForegroundColor = GridRiver; Console.Write("◄ "); break;
                        case 44: Console.ForegroundColor = GridRiver; Console.Write("║ "); break;
                        case 45: Console.ForegroundColor = GridRiver; Console.Write("══"); break;
                        case 46: Console.ForegroundColor = GridRiver; Console.Write("╚═"); break;
                        case 47: Console.ForegroundColor = GridRiver; Console.Write("╔═"); break;
                        case 48: Console.ForegroundColor = GridRiver; Console.Write("╗ "); break;
                        case 49: Console.ForegroundColor = GridRiver; Console.Write("╝ "); break;
                        case 50: Console.ForegroundColor = GridRiver; Console.Write("╩═"); break;
                        case 51: Console.ForegroundColor = GridRiver; Console.Write("╠═"); break;
                        case 52: Console.ForegroundColor = GridRiver; Console.Write("╦═"); break;
                        case 53: Console.ForegroundColor = GridRiver; Console.Write("╣ "); break;
                        case 54: Console.ForegroundColor = GridRiver; Console.Write("╬═"); break;
                        case 55: Console.ForegroundColor = GridRiver; Console.Write("· "); break;
                        case 56: Console.ForegroundColor = GridDecoration; Console.Write("@ "); break;

                        default: Console.ResetColor(); Console.Write("??"); break;
                    }

                }
                Console.ForegroundColor = GridBorder;
                Console.Write("# ");
                Console.ForegroundColor = GridCoords;
                Console.WriteLine(i%10);
            }

            Console.Write("  ");
            Console.ForegroundColor = GridBorder;
            for(int i = 0;i<sizeX + 1;i++){
                Console.Write("##");
            }
            Console.WriteLine("#");
            Console.Write("    ");

            Console.ForegroundColor = GridCoords;
            for(int i = 0;i<sizeX;i++){
                Console.Write(i%10 + " ");
            }
            Console.ResetColor();
            Console.WriteLine();
        }

        public static void SetRoad(){
            for(int x = 0;x<sizeY;x++){
                for(int y = 0;y<sizeX;y++){
                    if(grid[y,x] == 2 || grid[y,x] == 4){

                        if(!IsRoadIndex(y-1,x) && !IsRoadIndex(y,x+1) && !IsRoadIndex(y+1,x) && IsRoadIndex(y,x-1)){
                                grid[y,x] = 10;
                            }
                        else if(!IsRoadIndex(y-1,x) && !IsRoadIndex(y,x+1) && IsRoadIndex(y+1,x) && !IsRoadIndex(y,x-1)){
                                grid[y,x] = 11;
                            }
                        else if(!IsRoadIndex(y-1,x) && IsRoadIndex(y,x+1) && !IsRoadIndex(y+1,x) && !IsRoadIndex(y,x-1)){
                                grid[y,x] = 12;
                            }
                        else if(IsRoadIndex(y-1,x) && !IsRoadIndex(y,x+1) && !IsRoadIndex(y+1,x) && !IsRoadIndex(y,x-1)){
                                grid[y,x] = 13;
                            }
                        else if(!IsRoadIndex(y-1,x) && IsRoadIndex(y,x+1) && !IsRoadIndex(y+1,x) && IsRoadIndex(y,x-1)){
                                grid[y,x] = 14;
                            }
                        else if(IsRoadIndex(y-1,x) && !IsRoadIndex(y,x+1) && IsRoadIndex(y+1,x) && !IsRoadIndex(y,x-1)){
                                grid[y,x] = 15;
                            }
                        else if(!IsRoadIndex(y-1,x) && !IsRoadIndex(y,x+1) && IsRoadIndex(y+1,x) && IsRoadIndex(y,x-1)){
                                grid[y,x] = 16;
                            }
                        else if(!IsRoadIndex(y-1,x) && IsRoadIndex(y,x+1) && IsRoadIndex(y+1,x) && !IsRoadIndex(y,x-1)){
                                grid[y,x] = 17;
                            }
                        else if(IsRoadIndex(y-1,x) && IsRoadIndex(y,x+1) && !IsRoadIndex(y+1,x) && !IsRoadIndex(y,x-1)){
                                grid[y,x] = 18;
                            }
                        else if(IsRoadIndex(y-1,x) && !IsRoadIndex(y,x+1) && !IsRoadIndex(y+1,x) && IsRoadIndex(y,x-1)){
                                grid[y,x] = 19;
                            }
                        else if(IsRoadIndex(y-1,x) && !IsRoadIndex(y,x+1) && IsRoadIndex(y+1,x) && IsRoadIndex(y,x-1)){
                                grid[y,x] = 20;
                            }
                        else if(!IsRoadIndex(y-1,x) && IsRoadIndex(y,x+1) && IsRoadIndex(y+1,x) && IsRoadIndex(y,x-1)){
                                grid[y,x] = 21;
                            }
                        else if(IsRoadIndex(y-1,x) && IsRoadIndex(y,x+1) && IsRoadIndex(y+1,x) && !IsRoadIndex(y,x-1)){
                                grid[y,x] = 22;
                            }
                        else if(IsRoadIndex(y-1,x) && IsRoadIndex(y,x+1) && !IsRoadIndex(y+1,x) && IsRoadIndex(y,x-1)){
                                grid[y,x] = 23;
                            }
                        else if(IsRoadIndex(y-1,x) && IsRoadIndex(y,x+1) && IsRoadIndex(y+1,x) && IsRoadIndex(y,x-1)){
                                grid[y,x] = 24;
                            }
                        else{
                            grid[y,x] = 25;
                        }
                    }
                    else if(grid[y,x] == 7){
                        if(IsRoadIndex(y,x-1))
                                grid[y,x] = 26;
                        else if(IsRoadIndex(y+1,x))
                                grid[y,x] = 27;
                        else if(IsRoadIndex(y,x+1))
                                grid[y,x] = 28;
                        else if(IsRoadIndex(y-1,x))
                                grid[y,x] = 29;
                    }
                    else if(grid[y,x] == 8){

                        if(!IsRiverIndex(y-1,x) && !IsRiverIndex(y,x+1) && !IsRiverIndex(y+1,x) && IsRiverIndex(y,x-1)){
                                grid[y,x] = 40;
                            }
                        else if(!IsRiverIndex(y-1,x) && !IsRiverIndex(y,x+1) && IsRiverIndex(y+1,x) && !IsRiverIndex(y,x-1)){
                                grid[y,x] = 41;
                            }
                        else if(!IsRiverIndex(y-1,x) && IsRiverIndex(y,x+1) && !IsRiverIndex(y+1,x) && !IsRiverIndex(y,x-1)){
                                grid[y,x] = 42;
                            }
                        else if(IsRiverIndex(y-1,x) && !IsRiverIndex(y,x+1) && !IsRiverIndex(y+1,x) && !IsRiverIndex(y,x-1)){
                                grid[y,x] = 43;
                            }
                        else if(!IsRiverIndex(y-1,x) && IsRiverIndex(y,x+1) && !IsRiverIndex(y+1,x) && IsRiverIndex(y,x-1)){
                                grid[y,x] = 44;
                            }
                        else if(IsRiverIndex(y-1,x) && !IsRiverIndex(y,x+1) && IsRiverIndex(y+1,x) && !IsRiverIndex(y,x-1)){
                                grid[y,x] = 45;
                            }
                        else if(!IsRiverIndex(y-1,x) && !IsRiverIndex(y,x+1) && IsRiverIndex(y+1,x) && IsRiverIndex(y,x-1)){
                                grid[y,x] = 46;
                            }
                        else if(!IsRiverIndex(y-1,x) && IsRiverIndex(y,x+1) && IsRiverIndex(y+1,x) && !IsRiverIndex(y,x-1)){
                                grid[y,x] = 47;
                            }
                        else if(IsRiverIndex(y-1,x) && IsRiverIndex(y,x+1) && !IsRiverIndex(y+1,x) && !IsRiverIndex(y,x-1)){
                                grid[y,x] = 48;
                            }
                        else if(IsRiverIndex(y-1,x) && !IsRiverIndex(y,x+1) && !IsRiverIndex(y+1,x) && IsRiverIndex(y,x-1)){
                                grid[y,x] = 49;
                            }
                        else if(IsRiverIndex(y-1,x) && !IsRiverIndex(y,x+1) && IsRiverIndex(y+1,x) && IsRiverIndex(y,x-1)){
                                grid[y,x] = 50;
                            }
                        else if(!IsRiverIndex(y-1,x) && IsRiverIndex(y,x+1) && IsRiverIndex(y+1,x) && IsRiverIndex(y,x-1)){
                                grid[y,x] = 51;
                            }
                        else if(IsRiverIndex(y-1,x) && IsRiverIndex(y,x+1) && IsRiverIndex(y+1,x) && !IsRiverIndex(y,x-1)){
                                grid[y,x] = 52;
                            }
                        else if(IsRiverIndex(y-1,x) && IsRiverIndex(y,x+1) && !IsRiverIndex(y+1,x) && IsRiverIndex(y,x-1)){
                                grid[y,x] = 53;
                            }
                        else if(IsRiverIndex(y-1,x) && IsRiverIndex(y,x+1) && IsRiverIndex(y+1,x) && IsRiverIndex(y,x-1)){
                                grid[y,x] = 54;
                            }
                        else{
                            grid[y,x] = 55;
                        }
                    }
                }
            }
        }
        public static bool IsRoadIndex(int x, int y){
            return x >=0 && x < sizeX && y>=0 && y < sizeY && (grid[x,y] == 2 ||grid[x,y] == 7||grid[x,y] == 4 || (grid[x,y] >= 10 && grid[x,y] <=31));
        }
        public static bool IsRiverIndex(int x, int y){
            return x >=0 && x < sizeX && y>=0 && y < sizeY && (grid[x,y] == 8 || grid[x,y] == 31 || grid[x,y] == 30 || (grid[x,y] >= 40 && grid[x,y] <= 55));
        }

    }

}
