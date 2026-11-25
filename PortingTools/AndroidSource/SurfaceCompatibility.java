package com.klever.dpvr.compat;

import android.app.Activity;
import android.util.Log;
import android.view.SurfaceView;
import android.view.SurfaceHolder;
import android.view.Surface;
import android.view.View;
import android.view.ViewGroup;
import android.widget.FrameLayout;
import java.lang.reflect.Field;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;

/** Compatibility with SDKs expecting UnityPlayer.getChildAt(0) to be a SurfaceView. */
public final class SurfaceCompatibility {
    private static SurfaceView findSurface(View view) {
        if (view instanceof SurfaceView) return (SurfaceView)view;
        if (view instanceof ViewGroup) {
            ViewGroup group = (ViewGroup)view;
            for (int i = 0; i < group.getChildCount(); i++) {
                SurfaceView found = findSurface(group.getChildAt(i));
                if (found != null) return found;
            }
        }
        return null;
    }

    public static boolean prepare(final Activity activity) {
        final CountDownLatch completed = new CountDownLatch(1);
        final boolean[] success = new boolean[1];
        activity.runOnUiThread(new Runnable() {
            public void run() {
                try {
                    Field field = null;
                    for (Class<?> type = activity.getClass(); type != null && field == null; type = type.getSuperclass()) {
                        try { field = type.getDeclaredField("mUnityPlayer"); }
                        catch (NoSuchFieldException ignored) { }
                    }
                    if (field == null) throw new IllegalStateException("mUnityPlayer not found");
                    field.setAccessible(true);
                    ViewGroup player = (ViewGroup)field.get(activity);
                    SurfaceView originalSurface = findSurface(player);
                    if (originalSurface == null) throw new IllegalStateException("Unity SurfaceView not found");
                    Log.i("LegacySurface", "Before: firstChild=" + player.getChildAt(0).getClass().getName() + ", surface=" + originalSurface.getClass().getName());
                    final ViewGroup unityPlayer = player;
                    final java.lang.reflect.Method displayChanged = player.getClass().getMethod("displayChanged", int.class, Surface.class);
                    // Preserve the original hierarchy; expose a plain SurfaceView for the
                    // legacy runtime's exact-class check and redirect Unity's public display API.
                    View originalRoot = player.getChildAt(0);
                    originalRoot.setVisibility(View.GONE);
                    final SurfaceView compatibleSurface = new SurfaceView(activity);
                    compatibleSurface.getHolder().addCallback(new SurfaceHolder.Callback() {
                        private boolean connected;
                        public void surfaceCreated(SurfaceHolder holder) { connect(holder); }
                        public void surfaceChanged(SurfaceHolder holder, int format, int width, int height) { connect(holder); }
                        public void surfaceDestroyed(SurfaceHolder holder) {
                            connected = false;
                            // Passing null would return ownership to Unity's original
                            // view and mutate the hierarchy during Android layout.
                            // Keep external ownership; reconnect the recreated surface.
                            Log.i("LegacySurface", "Surface destroyed; retain external-display ownership until recreation");
                        }
                        private void connect(SurfaceHolder holder) {
                            final Surface surface = holder.getSurface();
                            if (!surface.isValid() || connected) return;
                            connected = true;
                            try {
                                Object result = displayChanged.invoke(unityPlayer, 0, surface);
                                Log.i("LegacySurface", "Unity external display connected=" + result + ", valid=" + surface.isValid());
                            } catch (Exception error) {
                                connected = false;
                                Log.e("LegacySurface", "Display attach failed", error);
                            }
                        }
                    });
                    player.addView(compatibleSurface, 0, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));
                    success[0] = player.getChildAt(0) == compatibleSurface;
                    Log.i("LegacySurface", "Plain SurfaceView exposed at child zero=" + success[0]);
                } catch (Exception error) {
                    Log.e("LegacySurface", "Unable to prepare legacy surface", error);
                } finally { completed.countDown(); }
            }
        });
        try {
            if (!completed.await(5, TimeUnit.SECONDS)) Log.e("LegacySurface", "UI-thread surface preparation timed out");
        } catch (InterruptedException interrupted) { Thread.currentThread().interrupt(); }
        return success[0];
    }
}
