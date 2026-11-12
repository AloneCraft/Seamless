package dev.seamless.datagrip;

import com.intellij.database.Dbms;
import javax.swing.ImageIcon;
import java.awt.image.BufferedImage;

/** Independent product identity. No Microsoft DBMS or system-catalog emulation. */
public final class SeamlessDbms {
    public static final Dbms INSTANCE = Dbms.create("SEAMLESS", "SeamlessDB",
        () -> new ImageIcon(new BufferedImage(16, 16, BufferedImage.TYPE_INT_ARGB)),
        "(?i)(?:jdbc:seamless://.*|dev\\.seamless\\.jdbc\\.SeamlessDriver|SeamlessDB)");
    private SeamlessDbms() {}
}
