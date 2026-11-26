package com.klever.dpvr;

import android.app.Activity;
import android.app.ActivityManager;
import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.drawable.Drawable;
import android.util.Log;

public final class KleverRecents {
    private KleverRecents() {}

    @SuppressWarnings("deprecation")
    public static void apply(Activity activity) {
        activity.runOnUiThread(() -> {
            try {
                Drawable icon = activity.getApplicationInfo().loadIcon(activity.getPackageManager());
                Bitmap bitmap = Bitmap.createBitmap(96, 96, Bitmap.Config.ARGB_8888);
                icon.setBounds(0, 0, bitmap.getWidth(), bitmap.getHeight());
                icon.draw(new Canvas(bitmap));
                String label = activity.getApplicationInfo().loadLabel(activity.getPackageManager()).toString();
                activity.setTaskDescription(new ActivityManager.TaskDescription(label, bitmap, Color.rgb(9, 9, 9)));
                Log.i("KleverRecents", "Applied application icon to the Android task.");
            } catch (RuntimeException error) {
                Log.w("KleverRecents", "Unable to set the Android task icon.", error);
            }
        });
    }
}
