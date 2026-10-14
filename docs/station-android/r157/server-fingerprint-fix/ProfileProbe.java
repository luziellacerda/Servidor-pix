import java.lang.reflect.*;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import dalvik.system.PathClassLoader;
import org.json.*;

/** Read-only validation using the actual installed DEX and public profile fixture. */
public final class ProfileProbe {
    public static void main(String[] args) throws Exception {
        ClassLoader loader = new PathClassLoader(args[0], ProfileProbe.class.getClassLoader());
        Class<?> profile = Class.forName("org.emulationstation.frontend.netplay.StationMultiplayerProfile", true, loader);
        JSONObject view = new JSONObject(new String(Files.readAllBytes(Paths.get(args[1])), StandardCharsets.UTF_8));
        JSONObject engine = new JSONObject(new String(Files.readAllBytes(Paths.get(args[2])), StandardCharsets.UTF_8));
        Method canonical = profile.getDeclaredMethod("canonical", String.class, int[].class, String.class);
        canonical.setAccessible(true);
        String value = (String) canonical.invoke(null, "direct-four-ports-v1", new int[]{1,1,1,1}, engine.getString("options"));
        StringBuilder digest = new StringBuilder();
        for (byte b : java.security.MessageDigest.getInstance("SHA-256").digest(value.getBytes(StandardCharsets.UTF_8)))
            digest.append(String.format(java.util.Locale.ROOT, "%02x", b & 255));
        System.out.println("ANDROID_CANONICAL " + value);
        System.out.println("ANDROID_CANONICAL_SHA256 " + digest);
        Constructor<?> ctor = profile.getDeclaredConstructor(JSONObject.class, JSONObject.class);
        ctor.setAccessible(true);
        JSONArray rows = view.getJSONArray("profiles");
        int failed = 0;
        java.util.Set<String> items = new java.util.TreeSet<>();
        for (int i = 0; i < rows.length(); i++) {
            JSONObject row = rows.getJSONObject(i);
            items.add(row.getString("itemId"));
            try {
                ctor.newInstance(row, engine);
                System.out.println("PROFILE PASS " + row.getString("profileId"));
            } catch (InvocationTargetException ex) {
                failed++;
                System.out.println("PROFILE FAIL " + row.getString("profileId"));
                ex.getCause().printStackTrace(System.out);
            }
        }
        Method choices = profile.getDeclaredMethod("choices", JSONObject.class, String.class, JSONObject.class);
        choices.setAccessible(true);
        try {
            Object result = choices.invoke(null, view, args[3], engine);
            System.out.println("CHOICES PASS " + ((java.util.List<?>) result).size());
        } catch (InvocationTargetException ex) {
            System.out.println("CHOICES FAIL");
            ex.getCause().printStackTrace(System.out);
        }
        int classifiedItems = 0;
        int onlineItems = 0;
        for (String item : items) {
            try {
                java.util.List<?> modes = (java.util.List<?>) choices.invoke(null, view, item, engine);
                classifiedItems++;
                if (!modes.isEmpty()) onlineItems++;
            } catch (InvocationTargetException ex) {
                failed++;
                System.out.println("ITEM FAIL " + item);
                ex.getCause().printStackTrace(System.out);
            }
        }
        System.out.println("SUMMARY profiles=" + rows.length() + " items=" + classifiedItems + " onlineItems=" + onlineItems + " failures=" + failed);
        if (failed != 0) System.exit(2);
    }
}
