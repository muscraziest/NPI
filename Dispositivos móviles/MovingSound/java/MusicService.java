package com.example.laura.movingsound;

import java.util.ArrayList;

import android.app.Service;
import android.content.ContentUris;
import android.content.Intent;
import android.media.AudioManager;
import android.media.MediaPlayer;
import android.net.Uri;
import android.os.Binder;
import android.os.Build;
import android.os.IBinder;
import android.os.PowerManager;
import android.support.annotation.RequiresApi;
import android.util.Log;
import java.util.Random;
import android.app.Notification;
import android.app.PendingIntent;
import android.widget.Toast;

/**
 * Created by Laura on 12/01/2017.
 */

public class MusicService extends Service implements MediaPlayer.OnPreparedListener, MediaPlayer.OnErrorListener, MediaPlayer.OnCompletionListener {

    //Reproductor
    private MediaPlayer player;
    //Lista de canciones
    private ArrayList<Song> songs;
    //Posición actual
    private int songPosn;
    private final IBinder musicBind = new MusicBinder();
    private boolean shuffle = false;
    private boolean replay = false;
    private boolean shuffle_state  = false;
    private String songTitle = "";
    private static final int NOTIFY_ID=1;
    private Random rand;

    public void onCreate(){
        //Creamos el servicio
        super.onCreate();
        //Inicializamos la posición
        songPosn=0;
        rand=new Random();
        //Creamos el reproductor
        player = new MediaPlayer();

        initMusicPlayer();
    }

    @Override
    public void onDestroy() {
        stopForeground(true);
    }

    //Inicializamos el reproductor
    public void initMusicPlayer(){
        player.setWakeMode(getApplicationContext(),
                PowerManager.PARTIAL_WAKE_LOCK);
        player.setAudioStreamType(AudioManager.STREAM_MUSIC);
        player.setOnPreparedListener(this);
        player.setOnCompletionListener(this);
        player.setOnErrorListener(this);
    }

    public void setList(ArrayList<Song> theSongs){
        songs=theSongs;
    }

    public class MusicBinder extends Binder {
        MusicService getService() {
            return MusicService.this;
        }
    }

    @Override
    public IBinder onBind(Intent intent) {
        return musicBind;
    }

    @Override
    public boolean onUnbind(Intent intent){
        player.stop();
        player.release();
        return false;
    }

    @Override
    public void onCompletion(MediaPlayer mp) {
        if(player.getCurrentPosition() > 0){
            mp.reset();
            playNext();
        }
    }

    @Override
    public boolean onError(MediaPlayer mp, int what, int extra) {
        mp.reset();
        return false;
    }

    @RequiresApi(api = Build.VERSION_CODES.JELLY_BEAN)
    @Override
    public void onPrepared(MediaPlayer mp) {
        //Iniciamosla reproducción
        mp.start();
        Intent notIntent = new Intent(this, MainActivity.class);
        notIntent.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP);
        PendingIntent pendInt = PendingIntent.getActivity(this, 0,
                notIntent, PendingIntent.FLAG_UPDATE_CURRENT);

        Notification.Builder builder = new Notification.Builder(this);

        builder.setContentIntent(pendInt)
                .setSmallIcon(R.drawable.play)
                .setTicker(songTitle)
                .setOngoing(true)
                .setContentTitle("Playing")
        .setContentText(songTitle);
        Notification not = builder.build();

        startForeground(NOTIFY_ID, not);
    }

    public void playSong(){
        player.reset();
        //Obtenemos la canción
        Song playSong = songs.get(songPosn);
        songTitle=playSong.getTitle();
        //Obtenemos su ID
        long currSong = playSong.getID();
        //Obtenemos su Uri
        Uri trackUri = ContentUris.withAppendedId(
                android.provider.MediaStore.Audio.Media.EXTERNAL_CONTENT_URI,
                currSong);
        try{
            player.setDataSource(getApplicationContext(), trackUri);
        }
        catch(Exception e){
            Log.e("MUSIC SERVICE", "Error setting data source", e);
        }

        player.prepareAsync();
    }

    public boolean playSong(String name){
        player.reset();
        boolean exists = false;
        Song playSong;

        //Buscamos la canción por su nombre en la lista
        for(int i=0; i < songs.size() && !exists; ++i){

            //Si encontramos la canción, la reproducimos
            if(songs.get(songPosn).getTitle().toLowerCase().equals(name.toLowerCase())) {
                playSong = songs.get(songPosn);
                songTitle = name;
                exists = true;

                long currSong = playSong.getID();
                Uri trackUri = ContentUris.withAppendedId(
                        android.provider.MediaStore.Audio.Media.EXTERNAL_CONTENT_URI,
                        currSong);
                try {
                    player.setDataSource(getApplicationContext(), trackUri);
                } catch (Exception e) {
                    Log.e("MUSIC SERVICE", "Error setting data source", e);
                }

                player.prepareAsync();
            }

            else {
                songPosn++;
                if (songPosn >= songs.size())
                    songPosn = 0;
            }
        }

        //Devólvemos el resultado de la búsqueda
        return exists;
    }


    public void setSong(int songIndex){

        songPosn=songIndex;
    }

    public int getPosn(){

        return player.getCurrentPosition();
    }

    public int getDur(){

        return player.getDuration();
    }

    public boolean isPng(){

        return player.isPlaying();
    }

    public void pausePlayer(){

        player.pause();
    }

    public void seek(int posn){

        player.seekTo(posn);
    }

    public void go(){

        player.start();
    }

    public void playPrev(){

        if(replay){
            int newSong = songPosn;
            songPosn = newSong;
        }
        else if (shuffle){

            int newSong = songPosn;
            while(newSong==songPosn){
                newSong=rand.nextInt(songs.size());
            }
            songPosn=newSong;
        }
        else {
            songPosn--;
            if (songPosn < 0)
                songPosn = songs.size() - 1;
        }
        playSong();
    }

    public void playNext(){
        if(shuffle){
            int newSong = songPosn;
            while(newSong==songPosn){
                newSong=rand.nextInt(songs.size());
            }
            songPosn=newSong;
        }

        else if(replay){
            int newSong = songPosn;
            songPosn = newSong;
        }

        else{
            songPosn++;
            if(songPosn >= songs.size())
                songPosn=0;
        }
        playSong();
    }

    public void setShuffle(){

        if(shuffle) {
            shuffle = false;
            shuffle_state = false;
            Toast.makeText(this, "Shuffle: desactivado", Toast.LENGTH_SHORT).show();
        }
        else {
            shuffle = true;
            shuffle_state = true;
            Toast.makeText(this, "Shuffle: activado", Toast.LENGTH_SHORT).show();
        }
    }

    public void setReplay(){

        if(replay) {
            replay = false;
            Toast.makeText(this, "Replay: desactivado", Toast.LENGTH_SHORT).show();

            //Si estaba activado el modo shuffle, lo dejamos activado
            if(shuffle_state)
                shuffle = true;
            //Si no, lo desactivamos
            else
                shuffle = false;
        }
        else {
            replay = true;
            //Si activamos replay, desactivamos shuffle
            shuffle = false;
            Toast.makeText(this, "Replay: activado", Toast.LENGTH_SHORT).show();
        }
    }
    public boolean getShuffle(){

        return shuffle;
    }

    public boolean getReplay(){

        return replay;
    }
}
